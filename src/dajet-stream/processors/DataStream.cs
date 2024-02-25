using DaJet.Scripting;
using DaJet.Scripting.Model;

namespace DaJet.Stream
{
    public sealed class DataStream : IProcessor
    {
        private readonly IProcessor _next;
        private readonly ScriptScope _scope;
        public DataStream(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ScriptModel)
            {
                throw new InvalidOperationException();
            }

            _next = StreamFactory.Create(_scope.Children);

            foreach (var declare in scope.Variables)
            {
                //TODO: initialize stream context
                // see ConfigureParameters !!!
            }
        }
        public void LinkTo(in IProcessor next) { throw new NotImplementedException(); }
        public void Synchronize() { _next?.Synchronize(); }
        public void Dispose() { _next?.Dispose(); }
        public void Process() { _next?.Process(); }
    }
}