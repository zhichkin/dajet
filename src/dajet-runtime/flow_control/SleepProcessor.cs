using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class SleepProcessor : IProcessor
    {
        private IProcessor _next;
        private readonly ScriptScope _scope;
        private readonly SleepStatement _statement;
        public SleepProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not SleepStatement statement)
            {
                throw new ArgumentException(nameof(SleepStatement));
            }

            _statement = statement;
        }
        public void Dispose() { _next?.Dispose(); }
        public void Synchronize() { _next?.Synchronize(); }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Process()
        {
            //TODO: [SLEEP] Task.Delay(timeout).Wait(cancellation_token);

            //try
            //{
            //    Task.Delay(TimeSpan.FromSeconds(_statement.Timeout)).Wait(_cancellationToken);
            //}
            //catch // (OperationCanceledException)
            //{
            //    // do nothing - host shutdown requested
            //}

            Thread.Sleep(TimeSpan.FromSeconds(_statement.Timeout));

            _next?.Process();
        }
    }
}