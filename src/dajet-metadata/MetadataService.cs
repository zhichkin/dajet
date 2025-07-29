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
using System.Threading;

namespace DaJet.Metadata
{
    public sealed class MetadataService : IMetadataService
    {
        private const string ERROR_CASH_ENTRY_KEY_IS_NOT_FOUND = "Cache entry key [{0}] is not found.";
        private const string ERROR_UNSUPPORTED_DATABASE_PROVIDER = "Unsupported database provider: [{0}].";
        private const string ERROR_UNSUPPORTED_METADATA_PROVIDER = "Unsupported metadata provider: [{0}].";
        private const string ERROR_CASH_ENTRY_IS_NULL_OR_EXPIRED = "Cache entry [{0}] is null or expired.";

        //NOTE: DefaultCapacity = 31
        //NOTE: DefaultConcurrencyLevel = Environment.ProcessorCount
        private readonly object _cache_lock = new();
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

        private static readonly MetadataService _singleton = new();
        public static MetadataService Cache { get { return _singleton; } }

        public void Add(InfoBaseOptions options)
        {
            _ = _cache.TryAdd(options.CacheKey, new CacheEntry(options));
        }
        public void Remove(string cacheKey)
        {
            if (_cache.TryRemove(cacheKey, out CacheEntry entry))
            {
                entry?.Dispose();
            }
        }
        public void Dispose()
        {
            foreach (CacheEntry entry in _cache.Values)
            {
                entry?.Dispose();
            }
            _cache.Clear();
        }

        public bool TryGetInfoBase(in InfoBaseRecord record, out InfoBase infoBase, out string error)
        {
            infoBase = null;
            error = string.Empty;

            if (!Enum.TryParse(record.DatabaseProvider, out DatabaseProvider provider))
            {
                error = $"Unsupported database provider: {record.DatabaseProvider}";
                return false;
            }

            try
            {
                Guid root;
                string connectionString = record.ConnectionString;

                using (ConfigFileReader reader = new(provider, connectionString, ConfigTables.Config, ConfigFiles.Root))
                {
                    root = new RootFileParser().Parse(in reader);
                }

                using (ConfigFileReader reader = new(provider, connectionString, ConfigTables.Config, root))
                {
                    new InfoBaseParser(null).Parse(in reader, root, out infoBase);
                }
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessageAndStackTrace(exception);
                return false;
            }

            return (infoBase is not null);
        }

        private CacheEntry GetOrAdd(in InfoBaseOptions options)
        {
            if (_cache.TryGetValue(options.CacheKey, out CacheEntry entry))
            {
                return entry; // fast path
            }

            bool lockTaken = false;

            try
            {
                Monitor.Enter(_cache_lock, ref lockTaken);

                if (_cache.TryGetValue(options.CacheKey, out entry))
                {
                    return entry; // double-checking (пока мы ждали _cache_lock, возможно другой поток уже обновил кэш)
                }

                entry = new CacheEntry(options); // создаём новый элемент кэша

                _ = _cache.TryAdd(options.CacheKey, entry); // добавляем новый элемент в кэш
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(_cache_lock);
                }
            }

            return entry;
        }
        public bool TryGetOrCreate(in InfoBaseRecord record, out IMetadataProvider provider, out string error)
        {
            provider = null;

            if (!Enum.TryParse(record.DatabaseProvider, out DatabaseProvider databaseProvider))
            {
                error = $"Unsupported database provider: {record.DatabaseProvider}";
                return false;
            }

            bool useExtensions = record.UseExtensions;
            string connectionString = record.ConnectionString;
            string cacheKey = DbConnectionFactory.GetCacheKey(databaseProvider, in connectionString, useExtensions);

            InfoBaseOptions options = new()
            {
                CacheKey = cacheKey,
                UseExtensions = useExtensions,
                DatabaseProvider = databaseProvider,
                ConnectionString = connectionString
            };

            if (!TryGetOrCreate(in options, out provider, out error))
            {
                return false;
            }

            return provider is not null;
        }
        public bool TryGetOrCreate(in InfoBaseOptions options, out IMetadataProvider provider, out string error)
        {
            error = string.Empty;

            //Метод GetOrAdd обеспечивает наличие элемента кэша, здесь он гарантированно не равен null
            CacheEntry entry = GetOrAdd(in options); // thread-safe

            //Согласованное чтение значений двух свойств одновременно,
            //иначе можно получить ссылку provider, а затем
            //прочитать значение expired для нового объекта provider,
            //который создаётся или обновляется далее по коду
            using (entry.ReadLock())
            {
                provider = entry.Value; // pin weak reference to stack before null-check
                bool expired = entry.IsExpired; // здесь уже может быть создан новый объект provider

                if (provider is not null && !expired)
                {
                    return true; // fast path
                }
            }

            //Для обновления значения кэша требуется эксклюзивная блокировка
            //Предварительно получаем блокировку обновления
            using (entry.UpdateLock())
            {
                provider = entry.Value; // pin weak reference to stack before null-check
                bool expired = entry.IsExpired;

                if (provider is not null && !expired) // double checking pattern
                {
                    return true; //Пока поток ожидал на блокировке, возможно другой поток уже обновил кэш
                }

                //Создаём новый элемент кэша один раз - только один поток может получить блокировку обновления
                try
                {
                    provider = CreateMetadataProvider(entry.Options);
                }
                catch (Exception exception)
                {
                    provider = null;
                    error = ExceptionHelper.GetErrorMessage(exception);
                    return false;
                }

                //Повышаем блокировку до эксклюзивной: дожидаемся пока все читатели снимут свои блокировки с элемента кэша
                using (entry.WriteLock())
                {
                    entry.Value = provider; //Присвоение значения свойству Value внутренне обновляет отметку времени последнего обновления
                }
            }

            return (provider is not null);
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
        private bool IsRegularDatabase(DatabaseProvider provider, string connectionString)
        {
            IQueryExecutor executor = CreateQueryExecutor(provider, connectionString);

            string script = SQLHelper.GetTableExistsScript("_yearoffset");

            return !(executor.ExecuteScalar<int>(in script, 10) == 1);
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
    }
}