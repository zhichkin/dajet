using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class SleepProcessor : IProcessor
    {
        private IProcessor _next;
        private readonly ScriptScope _scope;
        private readonly SleepStatement _statement;
        private CancellationTokenSource _cts;
        public SleepProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not SleepStatement statement)
            {
                throw new ArgumentException(nameof(SleepStatement));
            }

            _statement = statement;
        }
        public void Synchronize() { _next?.Synchronize(); }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Process()
        {
            //Thread.Sleep(TimeSpan.FromSeconds(_statement.Timeout));

            _cts ??= new CancellationTokenSource();

            CancellationToken token = _cts.Token;

            try
            {
                Task.Delay(TimeSpan.FromSeconds(_statement.Timeout)).Wait(token);
            }
            catch // (OperationCanceledException)
            {
                // do nothing - host shutdown requested
            }

            if (!token.IsCancellationRequested)
            {
                _next?.Process();
            }
        }
        public void Dispose()
        {
            try
            {
                _cts?.Cancel();
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }

            _next?.Dispose();
        }
    }
}