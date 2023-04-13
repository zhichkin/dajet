using System;
using System.Threading;

namespace DaJet.Metadata
{
    public sealed class RWLockSlim : IDisposable
    {
        private readonly ReaderWriterLockSlim _lock = new();
        public ReadLockToken ReadLock()
        {
            return new ReadLockToken(this);
        }
        public WriteLockToken WriteLock()
        {
            return new WriteLockToken(this);
        }
        public UpgradeableLockToken UpgradeableLock()
        {
            return new UpgradeableLockToken(this);
        }
        public void Dispose()
        {
            _lock.Dispose();
        }
        public readonly struct ReadLockToken : IDisposable
        {
            private readonly RWLockSlim _this;
            public ReadLockToken(in RWLockSlim @this)
            {
                _this = @this;
                _this._lock.EnterReadLock();
            }
            public void Dispose()
            {
                _this._lock.ExitReadLock();
            }
        }
        public readonly struct WriteLockToken : IDisposable
        {
            private readonly RWLockSlim _this;
            public WriteLockToken(in RWLockSlim @this)
            {
                _this = @this;
                _this._lock.EnterWriteLock();
            }
            public void Dispose()
            {
                _this._lock.ExitWriteLock();
            }
        }
        public readonly struct UpgradeableLockToken : IDisposable
        {
            private readonly RWLockSlim _this;
            public UpgradeableLockToken(in RWLockSlim @this)
            {
                _this = @this;
                _this._lock.EnterUpgradeableReadLock();
            }
            public void Dispose()
            {
                _this._lock.ExitUpgradeableReadLock();
            }
        }
    }
}