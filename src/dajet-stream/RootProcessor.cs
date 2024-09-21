using DaJet.Scripting.Model;

namespace DaJet.Stream
{
    public sealed class RootProcessor : IProcessor
    {
        private readonly IProcessor _next;
        private readonly StreamScope _scope;
        public RootProcessor(in StreamScope scope)
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
        public void Process()
        {
            try
            {
                _next?.Process();
            }
            catch (ReturnException)
            {
                return; //TODO: avoid exception hack !?
            }
            catch (Exception error)
            {
                if (StreamManager.IGNORE_ERRORS)
                {
                    throw; //NOTE: the script should handle errors itself
                }
                else
                {
                    FileLogger.Default.Write(error);
                }
            }
        }
        public object GetReturnValue()
        {
            return _scope.GetReturnValue();
        }
    }
}