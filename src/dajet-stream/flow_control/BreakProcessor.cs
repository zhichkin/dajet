using DaJet.Scripting.Model;

namespace DaJet.Stream
{
    public sealed class BreakException : Exception { }
    public sealed class BreakProcessor : IProcessor
    {
        private readonly StreamScope _scope;
        private readonly BreakStatement _statement;
        public BreakProcessor(in StreamScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not BreakStatement statement)
            {
                throw new ArgumentException(nameof(BreakStatement));
            }

            _statement = statement;
        }
        public void Dispose() { }
        public void Synchronize() { }
        public void LinkTo(in IProcessor next) { }
        public void Process() { throw new BreakException(); }
    }
}
