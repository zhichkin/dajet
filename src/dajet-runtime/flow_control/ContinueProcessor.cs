using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class ContinueException : Exception { }
    public sealed class ContinueProcessor : IProcessor
    {
        private readonly StreamScope _scope;
        private readonly ContinueStatement _statement;
        public ContinueProcessor(in StreamScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ContinueStatement statement)
            {
                throw new ArgumentException(nameof(ContinueStatement));
            }

            _statement = statement;
        }
        public void Dispose() { }
        public void Synchronize() { }
        public void LinkTo(in IProcessor next) { }
        public void Process() { throw new ContinueException(); }
    }
}