using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class TryProcessor : IProcessor
    {
        private IProcessor _next;
        private IProcessor _try_block;
        private IProcessor _catch_block;
        private IProcessor _finally_block;
        private readonly ScriptScope _scope;
        private readonly TryStatement _statement;
        public TryProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not TryStatement statement)
            {
                throw new ArgumentException(nameof(TryStatement));
            }

            _statement = statement;

            ScriptScope try_scope = _scope.Create(_statement.TRY);
            _try_block = StreamFactory.CreateStream(in try_scope);

            if (_statement.CATCH is not null)
            {
                ScriptScope catch_scope = _scope.Create(_statement.CATCH);
                _catch_block = StreamFactory.CreateStream(in catch_scope);
            }

            if (_statement.FINALLY is not null)
            {
                ScriptScope finally_scope = _scope.Create(_statement.FINALLY);
                _finally_block = StreamFactory.CreateStream(in finally_scope);
            }
        }
        public void Dispose() { _next?.Dispose(); }
        public void Synchronize() { _next?.Synchronize(); }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Process()
        {
            _scope.ErrorMessage = string.Empty;

            try
            {
                _try_block.Process();
            }
            catch (ReturnException)
            {
                throw; // bubble up return exception to the root processor
            }
            catch (Exception error)
            {
                _scope.ErrorMessage = ExceptionHelper.GetErrorMessage(error);

                _catch_block?.Process();
            }
            finally
            {
                _scope.ErrorMessage = string.Empty;

                _finally_block?.Process();
            }

            _next?.Process();
        }
    }
}