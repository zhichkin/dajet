namespace DaJet.Stream
{
    public sealed class ProgressTracker : IProgress<bool>, IDisposable
    {
        //private Guid _uuid; // transaction identifier
        internal int _total; // total number of commands 
        internal int _count; // successfull commands counter
        internal bool _success = true; // successfull synchronization flag
        private ManualResetEvent _synchronizer;
        public ProgressTracker() { }
        public void Track() { _total++; }
        public void Report(bool success)
        {
            if (!success)
            {
                _success = false;
                _synchronizer?.Set();
            }
            else if (Interlocked.Increment(ref _count) == _total)
            {
                _synchronizer?.Set();
            }
        }
        public void Synchronize()
        {
            if (_total == 0 || _total == _count)
            {
                return;
            }

            if (!_success) // В этой транзакции уже происходили ошибки =)
            {
                throw new InvalidOperationException();
            }
            
            ManualResetEvent synchronizer = new(false);

            if (Interlocked.CompareExchange(ref _synchronizer, synchronizer, null) is not null)
            {
                synchronizer.Dispose();
            }

            if (Interlocked.CompareExchange(ref _count, _count, _total) == _total)
            {
                return;
            }

            _ = _synchronizer.WaitOne(); //TODO: provide timeout !!!

            if (!_success)
            {
                throw new InvalidOperationException();
            }
        }
        public void Dispose()
        {
            try
            {
                _synchronizer?.Dispose();
            }
            finally
            {
                _synchronizer = null;
            }
        }
    }
}