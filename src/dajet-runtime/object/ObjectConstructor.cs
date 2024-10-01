using DaJet.Data;
using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class ObjectConstructor : IProcessor
    {
        private IProcessor _next;
        private readonly ScriptScope _scope;
        private readonly VariableReference _target;
        private readonly SelectStatement _statement;
        public ObjectConstructor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not SelectStatement statement)
            {
                throw new ArgumentException(nameof(SelectStatement));
            }

            _statement = statement;

            StreamFactory.BindVariables(in _scope);

            if (!StreamFactory.TryGetIntoVariable(in _statement, out _target))
            {
                throw new InvalidOperationException($"[CONSTRUCTOR] Target variable expected");
            }
        }
        public void Dispose() { _next?.Dispose(); }
        public void Synchronize() { _next?.Synchronize(); }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Process()
        {
            if (_statement.Expression is SelectExpression select)
            {
                DataObject value = StreamFactory.ConstructObject(in _scope, in select);

                if (!_scope.TrySetValue(_target.Identifier, value))
                {
                    throw new InvalidOperationException($"Failed to assign variable {_target}");
                }
            }

            _next?.Process();
        }
    }
}