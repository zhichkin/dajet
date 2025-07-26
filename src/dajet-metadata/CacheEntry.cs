using System;

namespace DaJet.Metadata
{
    internal sealed class CacheEntry
    {
        private const long EXPIRATION_TIMEOUT = 600000L; // milliseconds = 10 minutes

        //private readonly RWLockSlim _lock = new();
        private readonly InfoBaseOptions _options;
        private readonly WeakReference<IMetadataProvider> _value = new(null);
        private long _lastUpdate = 0L; // milliseconds
        internal CacheEntry(InfoBaseOptions options) { _options = options; }
        internal InfoBaseOptions Options { get { return _options; } }
        //internal RWLockSlim.UpgradeableLockToken UpdateLock() { return _lock.UpgradeableLock(); }
        internal IMetadataProvider Value
        {
            set
            {
                //NOTE: запись значения всегда выполняется классом MetadataService
                //NOTE: с использованием эксклюзивной блокировки на всём объекте
                //using (_lock.WriteLock())
                //{
                _value.SetTarget(value);
                _lastUpdate = Environment.TickCount64;
                //NOTE: Защита от оптимизаций компилятора и процессора:
                //NOTE: вероятно, что здесь порядок инструкций не так важен
                //Volatile.Write(ref _lastUpdate, Environment.TickCount64);
                //}
            }
            get
            {
                //NOTE: чтение ссылок всегда атомарно: нет причин защищать чтение от torn read
                //NOTE: кроме этого нет причин защищать одиночное чтение от нескольких потоков
                //using (_lock.ReadLock())
                //{
                    if (_value.TryGetTarget(out IMetadataProvider value))
                    {
                        return value;
                    }
                    else
                    {
                        return null;
                    }
                //}
            }
        }
        internal bool IsExpired
        {
            get
            {
                long elapsed = (Environment.TickCount64 - _lastUpdate);

                return EXPIRATION_TIMEOUT < elapsed;
            }
        }
        internal void Dispose()
        {
            //_lock.Dispose();
            _value.SetTarget(null);
            //NOTE: ссылка на _options может быть где угодно ...
            //NOTE: а от ConnectionString формируется ключ кэша ...
            //_options.ConnectionString = null;
        }
    }
}