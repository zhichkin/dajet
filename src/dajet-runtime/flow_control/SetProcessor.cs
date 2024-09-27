using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class SetProcessor : IProcessor
    {
        private IProcessor _next;
        private readonly ScriptScope _scope;
        private readonly string _target;
        private readonly AssignmentStatement _statement;
        public SetProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not AssignmentStatement statement)
            {
                throw new ArgumentException(nameof(AssignmentStatement));
            }

            _statement = statement;

            if (_statement.Target is not VariableReference variable)
            {
                throw new InvalidOperationException($"Target is not variable");
            }

            _target = variable.Identifier;
        }
        public void Dispose() { _next?.Dispose(); }
        public void Synchronize() { _next?.Synchronize(); }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Process()
        {
            if (StreamFactory.TryEvaluate(in _scope, _statement.Initializer, out object value))
            {
                if (!_scope.TrySetValue(in _target, in value))
                {
                    throw new InvalidOperationException($"Failed to assign variable {_target}");
                }
            }

            _next?.Process();
        }
    }
}