using DaJet.Scripting.Model;

namespace DaJet.Stream
{
    public sealed class CaseProcessor : IProcessor
    {
        private IProcessor _next;
        private IProcessor _else;
        private Dictionary<SyntaxNode, IProcessor> _case = new();
        private readonly StreamScope _scope;
        private readonly CaseStatement _statement;
        public CaseProcessor(in StreamScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not CaseStatement statement)
            {
                throw new ArgumentException(nameof(CaseStatement));
            }

            _statement = statement;

            foreach (WhenClause when in _statement.CASE)
            {
                if (when.THEN is StatementBlock block)
                {
                    StreamScope then_scope = _scope.Create(in block);

                    IProcessor processor = StreamFactory.CreateStream(in then_scope);
                    
                    _case.Add(when.WHEN, processor);
                }
            }

            if (_statement.ELSE is not null)
            {
                StreamScope else_scope = _scope.Create(_statement.ELSE);

                _else = StreamFactory.CreateStream(in else_scope);
            }
        }
        public void Dispose() { _next?.Dispose(); }
        public void Synchronize() { _next?.Synchronize(); }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Process()
        {
            bool do_else = true;

            foreach (var item in _case)
            {
                if (StreamFactory.Evaluate(in _scope, item.Key))
                {
                    do_else = false;

                    item.Value.Process();
                    
                    break;
                }
            }

            if (do_else)
            {
                _else?.Process();
            }

            _next?.Process();
        }
    }
}