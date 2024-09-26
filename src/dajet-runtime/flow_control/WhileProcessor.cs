using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class WhileProcessor : IProcessor
    {
        private IProcessor _next;
        private IProcessor _body;
        private readonly StreamScope _scope;
        private readonly WhileStatement _statement;
        public WhileProcessor(in StreamScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not WhileStatement statement)
            {
                throw new ArgumentException(nameof(WhileStatement));
            }

            _statement = statement;

            StreamScope body_scope = _scope.Create(_statement.Statements);

            _body = StreamFactory.CreateStream(in body_scope);
        }
        public void Dispose() { _next?.Dispose(); }
        public void Synchronize() { _next?.Synchronize(); }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Process()
        {
            while (ConditionIsTrue())
            {
                try
                {
                    _body.Process(); //THINK: break/continue - avoid exception hack !?
                }
                catch (BreakException) { break; }
                catch (ContinueException) { continue; }
            }

            _next?.Process();
        }
        private bool ConditionIsTrue()
        {
            if (_statement.Condition is null) { return true; }

            SyntaxNode expression = _statement.Condition;

            return StreamFactory.Evaluate(in _scope, in expression);
        }
    }
}