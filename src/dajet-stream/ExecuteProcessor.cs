using DaJet.Scripting.Model;

namespace DaJet.Stream
{
    public sealed class ExecuteProcessor : IProcessor
    {
        private IProcessor _next;
        private readonly StreamScope _scope;
        private readonly ExecuteStatement _statement;
        public ExecuteProcessor(in StreamScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ExecuteStatement statement)
            {
                throw new ArgumentException(nameof(ExecuteStatement));
            }

            _statement = statement;
        }
        public void Dispose() { _next?.Dispose(); }
        public void Synchronize() { _next?.Synchronize(); }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Process()
        {
            Console.WriteLine("*** EXECUTE ***");

            _next?.Process();
        }
    }
}