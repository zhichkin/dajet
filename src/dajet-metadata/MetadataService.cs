using DaJet.Data;
using DaJet.Data.PostgreSql;
using DaJet.Data.SqlServer;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Metadata.Parsers;
using DaJet.Metadata.SqlServer;
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
        private const string ERROR_CASH_ENTRY_INVALID_URI_PROVIDED = "Cache entry [{0}]: invalid URI provided.";

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

            if (provider is not MetadataCache metadata)
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
        public bool TryGetMetadataCache(string key, out MetadataCache cache, out string error)
        {
            if (!_cache.TryGetValue(key, out CacheEntry entry))
            {
                cache = null;
                error = string.Format(ERROR_CASH_ENTRY_KEY_IS_NOT_FOUND, key);
                return false;
            }

            error = string.Empty;
            cache = entry.Value as MetadataCache;

            if (cache != null && !entry.IsExpired)
            {
                return true;
            }

            using (entry.UpdateLock())
            {
                cache = entry.Value as MetadataCache;

                if (cache != null && !entry.IsExpired)
                {
                    return true;
                }

                cache = new MetadataCache(new MetadataCacheOptions()
                {
                    UseExtensions = entry.Options.UseExtensions,
                    DatabaseProvider = entry.Options.DatabaseProvider,
                    ConnectionString = entry.Options.ConnectionString
                });

                try
                {
                    cache.Initialize();
                }
                catch (Exception exception)
                {
                    cache = null;
                    error = ExceptionHelper.GetErrorMessage(exception);
                    return false;
                }

                // Assignment of the Value property internally refreshes the last update timestamp

                entry.Value = cache;
            }

            return (cache != null);
        }
        public bool TryGetQueryExecutor(string key, out IQueryExecutor executor, out string error)
        {
            error = string.Empty;

            if (!_cache.TryGetValue(key, out CacheEntry entry))
            {
                executor = null;

                error = string.Format(ERROR_CASH_ENTRY_KEY_IS_NOT_FOUND, key);

                return false;
            }

            if (entry.Options.DatabaseProvider == DatabaseProvider.SqlServer)
            {
                executor = new MsQueryExecutor(entry.Options.ConnectionString);
            }
            else if (entry.Options.DatabaseProvider == DatabaseProvider.PostgreSql)
            {
                executor = new PgQueryExecutor(entry.Options.ConnectionString);
            }
            else
            {
                executor = null;

                error = string.Format(ERROR_UNSUPPORTED_DATABASE_PROVIDER, entry.Options.DatabaseProvider);
            }

            return (executor != null);
        }
        
        public void Dispose()
        {
            foreach (CacheEntry entry in _cache.Values)
            {
                entry.Dispose();
            }
            _cache.Clear();
        }

        public bool IsRegularDatabase(string key)
        {
            if (!TryGetQueryExecutor(key, out IQueryExecutor executor, out string error))
            {
                throw new InvalidOperationException(error);
            }

            string script = SQLHelper.GetTableExistsScript("_yearoffset");

            return !(executor.ExecuteScalar<int>(in script, 10) == 1);
        }
        private IMetadataProvider CreateMetadataProvider(string connectionString)
        {
            //if (!Uri.TryCreate(connectionString, UriKind.Absolute, out Uri uri))
            //{
            //    throw new InvalidOperationException(string.Format(ERROR_CASH_ENTRY_INVALID_URI_PROVIDED, connectionString));
            //}

            //if (uri.Scheme != "mssql")
            //{
            //    throw new InvalidOperationException(string.Format(ERROR_UNSUPPORTED_METADATA_PROVIDER, uri.Scheme));
            //}

            //Dictionary<string, string> options = UriHelper.CreateOptions(in uri);

            Dictionary<string, string> options = new()
            {
                { "ConnectionString", connectionString }
            };

            IMetadataProvider provider = new MsMetadataProvider();

            provider.Configure(options);

            return provider;
        }
        public bool TryGetMetadataProvider(string key, out IMetadataProvider provider, out string error)
        {
            error = string.Empty;

            if (!_cache.TryGetValue(key, out CacheEntry entry))
            {
                provider = null;
                error = string.Format(ERROR_CASH_ENTRY_KEY_IS_NOT_FOUND, key);
                return false;
            }

            provider = entry.Value; // pin weak reference to stack

            if (provider is not null && !entry.IsExpired)
            {
                return true;
            }

            using (entry.UpdateLock())
            {
                provider = entry.Value; // pin weak reference to stack

                if (provider is not null && !entry.IsExpired)
                {
                    return true;
                }

                try
                {
                    provider = CreateMetadataProvider(entry.Options.ConnectionString);
                }
                catch (Exception exception)
                {
                    provider = null;
                    error = ExceptionHelper.GetErrorMessage(exception);
                    return false;
                }

                // assignment of the Value property internally refreshes the last update timestamp

                entry.Value = provider;
            }

            return (provider is not null);
        }
    }
}