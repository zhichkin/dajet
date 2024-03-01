using DaJet.Scripting.Model;

namespace DaJet.Stream
{
    public sealed class UpdateIntoArrayProcessor : IProcessor
    {
        private IProcessor _next;
        private readonly StreamScope _scope;
        public UpdateIntoArrayProcessor(in StreamScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            _ = _scope.GetParent<UseStatement>();
        }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Synchronize() { _next?.Synchronize(); }
        public void Dispose() { _next?.Dispose(); }
        public void Process() { _next?.Process(); }
    }
}