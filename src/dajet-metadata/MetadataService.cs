using DaJet.Data;
using DaJet.Data.PostgreSql;
using DaJet.Data.SqlServer;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Metadata.Parsers;
using DaJet.Metadata.SqlServer;
using DaJet.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DaJet.Metadata
{
    public sealed class MetadataService : IMetadataService
    {
        private const string ERROR_CASH_ENTRY_KEY_IS_NOT_FOUND = "Cache entry key [{0}] is not found.";
        private const string ERROR_UNSUPPORTED_DATABASE_PROVIDER = "Unsupported database provider: [{0}].";
        private const string ERROR_UNSUPPORTED_METADATA_PROVIDER = "Unsupported metadata provider: [{0}].";
        private const string ERROR_CASH_ENTRY_IS_NULL_OR_EXPIRED = "Cache entry [{0}] is null or expired.";

        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

        public List<InfoBaseOptions> Options
        {
            get
            {
                List<InfoBaseOptions> list = new();

                foreach (var item in _cache)
                {
                    list.Add(item.Value.Options);
                }

                return list;
            }
        }
        public void Add(InfoBaseOptions options)
        {
            _ = _cache.TryAdd(options.Key, new CacheEntry(options));
        }
        public void Remove(string key)
        {
            if (_cache.TryRemove(key, out CacheEntry entry))
            {
                entry?.Dispose();
            }
        }
        
        public bool TryGetInfoBase(string key, out InfoBase infoBase, out string error)
        {
            if (!_cache.TryGetValue(key, out CacheEntry entry))
            {
                infoBase = null;
                error = string.Format(ERROR_CASH_ENTRY_KEY_IS_NOT_FOUND, key);
                return false;
            }

            IMetadataProvider provider = entry.Value;

            if (provider is null)
            {
                infoBase = null;
                error = string.Format(ERROR_CASH_ENTRY_IS_NULL_OR_EXPIRED, key);
                return false;
            }

            if (provider is not OneDbMetadataProvider metadata)
            {
                infoBase = null;
                error = string.Format(ERROR_UNSUPPORTED_METADATA_PROVIDER, provider.GetType());
                return false;
            }

            Guid root;
            error = string.Empty;

            try
            {
                using (ConfigFileReader reader = new(
                    entry.Options.DatabaseProvider, entry.Options.ConnectionString, ConfigTables.Config, ConfigFiles.Root))
                {
                    root = new RootFileParser().Parse(in reader);
                }

                using (ConfigFileReader reader = new(
                    entry.Options.DatabaseProvider, entry.Options.ConnectionString, ConfigTables.Config, root))
                {
                    new InfoBaseParser(metadata).Parse(in reader, root, out infoBase);
                }
            }
            catch (Exception exception)
            {
                infoBase = null;
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return (infoBase != null);
        }
        public bool TryGetOneDbMetadataProvider(string key, out OneDbMetadataProvider metadata, out string error)
        {
            if (!_cache.TryGetValue(key, out CacheEntry entry))
            {
                metadata = null;
                error = string.Format(ERROR_CASH_ENTRY_KEY_IS_NOT_FOUND, key);
                return false;
            }

            error = string.Empty;
            metadata = entry.Value as OneDbMetadataProvider;

            if (metadata is not null && !entry.IsExpired)
            {
                return true;
            }

            using (entry.UpdateLock())
            {
                metadata = entry.Value as OneDbMetadataProvider;

                if (metadata is not null && !entry.IsExpired)
                {
                    return true;
                }

                metadata = new OneDbMetadataProvider(new OneDbMetadataProviderOptions()
                {
                    UseExtensions = entry.Options.UseExtensions,
                    DatabaseProvider = entry.Options.DatabaseProvider,
                    ConnectionString = entry.Options.ConnectionString
                });

                try
                {
                    metadata.Initialize();
                }
                catch (Exception exception)
                {
                    metadata = null;
                    error = ExceptionHelper.GetErrorMessage(exception);
                    return false;
                }

                // Assignment of the Value property internally refreshes the last update timestamp

                entry.Value = metadata;
            }

            return (metadata is not null);
        }
        public void Dispose()
        {
            foreach (CacheEntry entry in _cache.Values)
            {
                entry.Dispose();
            }
            _cache.Clear();
        }
        public bool TryGetMetadataProvider(string key, out IMetadataProvider metadata, out string error)
        {
            error = string.Empty;

            if (!_cache.TryGetValue(key, out CacheEntry entry))
            {
                metadata = null;
                error = string.Format(ERROR_CASH_ENTRY_KEY_IS_NOT_FOUND, key);
                return false;
            }

            metadata = entry.Value; // pin weak reference to stack

            if (metadata is not null && !entry.IsExpired)
            {
                return true;
            }

            using (entry.UpdateLock())
            {
                metadata = entry.Value; // pin weak reference to stack

                if (metadata is not null && !entry.IsExpired)
                {
                    return true;
                }

                try
                {
                    metadata = CreateMetadataProvider(entry.Options);
                }
                catch (Exception exception)
                {
                    metadata = null;
                    error = ExceptionHelper.GetErrorMessage(exception);
                    return false;
                }

                // assignment of the Value property internally refreshes the last update timestamp

                entry.Value = metadata;
            }

            return (metadata is not null);
        }
        private IQueryExecutor CreateQueryExecutor(DatabaseProvider provider, string connectionString)
        {
            if (provider == DatabaseProvider.SqlServer)
            {
                return new MsQueryExecutor(connectionString);
            }
            else if (provider == DatabaseProvider.PostgreSql)
            {
                return new PgQueryExecutor(connectionString);
            }

            throw new InvalidOperationException(string.Format(ERROR_UNSUPPORTED_DATABASE_PROVIDER, provider));
        }
        private bool IsRegularDatabase(DatabaseProvider provider, string connectionString)
        {
            IQueryExecutor executor = CreateQueryExecutor(provider, connectionString);

            string script = SQLHelper.GetTableExistsScript("_yearoffset");
            
            return !(executor.ExecuteScalar<int>(in script, 10) == 1);
        }
        private IMetadataProvider CreateOneDbMetadataProvider(in InfoBaseOptions options)
        {
            OneDbMetadataProvider metadata = new(new OneDbMetadataProviderOptions()
            {
                UseExtensions = options.UseExtensions,
                DatabaseProvider = options.DatabaseProvider,
                ConnectionString = options.ConnectionString
            });

            metadata.Initialize();

            return metadata;
        }
        private IMetadataProvider CreateMetadataProvider(in InfoBaseOptions options)
        {
            if (!IsRegularDatabase(options.DatabaseProvider, options.ConnectionString))
            {
                return CreateOneDbMetadataProvider(in options);
            }

            IMetadataProvider provider;

            if (options.DatabaseProvider == DatabaseProvider.SqlServer)
            {
                provider = new MsMetadataProvider(options.ConnectionString);
            }
            else if (options.DatabaseProvider == DatabaseProvider.PostgreSql)
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new InvalidOperationException(string.Format(ERROR_UNSUPPORTED_DATABASE_PROVIDER, options.DatabaseProvider));
            }

            return provider;
        }

        public static IMetadataProvider CreateOneDbMetadataProvider(in Uri uri)
        {
            return new OneDbMetadataProvider(in uri, false);
        }
        public static IMetadataProvider CreateOneDbMetadataProvider(in InfoBaseRecord options)
        {
            return new OneDbMetadataProvider(options.ConnectionString, options.UseExtensions);
        }
    }
}