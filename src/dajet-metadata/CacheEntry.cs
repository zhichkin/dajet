using System;
using System.Threading;

namespace DaJet.Metadata
{
    internal sealed class CacheEntry
    {
        private const long EXPIRATION_TIMEOUT = 600000L; // milliseconds = 10 minutes

        private readonly RWLockSlim _lock = new();
        private readonly InfoBaseOptions _options;
        private readonly WeakReference<IMetadataProvider> _value = new(null);
        private long _lastUpdate = 0L; // milliseconds
        internal CacheEntry(InfoBaseOptions options) { _options = options; }
        internal InfoBaseOptions Options { get { return _options; } }
        internal RWLockSlim.ReadLockToken ReadLock() { return _lock.ReadLock(); }
        internal RWLockSlim.WriteLockToken WriteLock() { return _lock.WriteLock(); }
        internal RWLockSlim.UpgradeableLockToken UpdateLock() { return _lock.UpgradeableLock(); }
        internal IMetadataProvider Value
        {
            get
            {
                //NOTE: чтение значения выполняется классом MetadataService,
                //NOTE: используя блокировку на чтение всего объекта

                if (_value.TryGetTarget(out IMetadataProvider value))
                {
                    return value;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                //NOTE: запись значения выполняется классом MetadataService,
                //NOTE: используя эксклюзивную блокировку всего объекта
                
                _value.SetTarget(value);

                if (IntPtr.Size == 8) // x64
                {
                    _lastUpdate = Environment.TickCount64;
                }
                else // x32 защита от возможного torn read
                {
                    Volatile.Write(ref _lastUpdate, Environment.TickCount64);
                }
            }
        }
        internal bool IsExpired
        {
            get
            {
                //NOTE: x32 защита от возможного torn read
                long lastUpdate = IntPtr.Size == 8
                    ? _lastUpdate
                    : Volatile.Read(ref _lastUpdate);

                long elapsed = (Environment.TickCount64 - lastUpdate);

                return EXPIRATION_TIMEOUT < elapsed;
            }
        }
        internal void Dispose()
        {
            _lock.Dispose();
            _value.SetTarget(null);
        }
    }
}