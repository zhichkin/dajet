using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class ReturnException : Exception { }
    public sealed class ReturnProcessor : IProcessor
    {
        private readonly StreamScope _scope;
        private readonly ReturnStatement _statement;
        public ReturnProcessor(in StreamScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ReturnStatement statement)
            {
                throw new ArgumentException(nameof(ReturnStatement));
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
                throw new InvalidOperationException($"Failed to evaluate return expression");
            }

            _scope.SetReturnValue(in value);

            throw new ReturnException();
        }
    }
}