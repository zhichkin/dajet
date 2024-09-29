using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class ThrowProcessor : IProcessor
    {
        private readonly ScriptScope _scope;
        private readonly ThrowStatement _statement;
        public ThrowProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ThrowStatement statement)
            {
                throw new ArgumentException(nameof(ThrowStatement));
            }

            StreamFactory.BindVariables(in _scope);

            _statement = statement;
        }
        public void Dispose() { }
        public void Synchronize() { }
        public void LinkTo(in IProcessor next) { }
        public void Process()
        {
            if (!StreamFactory.TryEvaluate(in _scope, _statement.Expression, out object value))
            {
                throw new InvalidOperationException($"Failed to evaluate throw expression");
            }

            if (value is not string message)
            {
                throw new InvalidOperationException($"Invalid throw expression type: string expected");
            }
            
            throw new Exception(message);
        }
    }
}