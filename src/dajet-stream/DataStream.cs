using DaJet.Scripting.Model;

namespace DaJet.Stream
{
    public sealed class DataStream : IProcessor
    {
        private readonly IProcessor _next;
        private readonly StreamScope _scope;
        public DataStream(in StreamScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ScriptModel)
            {
                throw new ArgumentException(nameof(ScriptModel));
            }

            StreamFactory.InitializeVariables(in _scope);

            _next = StreamFactory.CreateStream(in _scope);
        }
        public void LinkTo(in IProcessor next) { throw new NotImplementedException(); }
        public void Synchronize() { _next?.Synchronize(); }
        public void Dispose() { _next?.Dispose(); }
        public void Process() { _next?.Process(); }
    }
}