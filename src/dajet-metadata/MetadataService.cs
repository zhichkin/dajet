using DaJet.Data;
using DaJet.Data.PostgreSql;
using DaJet.Data.SqlServer;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Metadata.Parsers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DaJet.Metadata
{
    public interface IMetadataService : IDisposable
    {
        List<InfoBaseOptions> Options { get; }
        void Add(InfoBaseOptions options);
        void Remove(string key);

        bool TryGetInfoBase(string key, out InfoBase infoBase, out string error);
        bool TryGetMetadataCache(string key, out MetadataCache cache, out string error);
        bool TryGetQueryExecutor(string key, out IQueryExecutor executor, out string error);
    }
    public sealed class MetadataService : IMetadataService
    {
        private const string ERROR_CASH_ENTRY_KEY_IS_NOT_FOUND = "Cache entry key [{0}] is not found.";
        private const string ERROR_UNSUPPORTED_DATABASE_PROVIDER = "Unsupported database provider: [{0}].";

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
                    new InfoBaseParser().Parse(in reader, out infoBase);
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
            cache = entry.Value;

            if (cache != null && !entry.IsExpired)
            {
                return true;
            }

            using (entry.UpdateLock())
            {
                cache = entry.Value;

                if (cache != null && !entry.IsExpired)
                {
                    return true;
                }

                cache = new MetadataCache(new MetadataCacheOptions()
                {
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
    }
}