using DaJet.Scripting.Model;

namespace DaJet.Stream
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
            while (EvaluateCondition())
            {
                _body.Process();
            }

            _next?.Process();
        }
        private bool EvaluateCondition()
        {
            if (!StreamFactory.TryEvaluate(in _scope, _statement.Condition, out object value))
            {
                return false;
            }

            if (value is not bool condition)
            {
                return false;
            }

            return condition;
        }
    }
}