using DaJet.Scripting;
using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public abstract class UserDefinedProcessor : IProcessor
    {
        protected IProcessor _next;
        protected readonly ScriptScope _scope;
        protected readonly ProcessStatement _statement;
        protected readonly Dictionary<string, object> _options = new();
        protected UserDefinedProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ProcessStatement statement)
            {
                throw new ArgumentException(nameof(ProcessStatement));
            }

            StreamFactory.BindVariables(in _scope);

            _statement = statement;
            
            ConfigureReturnVariable();

            ConfigureProcessorOptions();
        }
        public void LinkTo(in IProcessor next) { _next = next; }
        public virtual void Synchronize() { _next?.Synchronize(); }
        public virtual void Dispose() { _next?.Dispose(); }
        public abstract void Process();
        private void ConfigureReturnVariable()
        {
            if (_statement.Return is not null)
            {
                string identifier = _statement.Return.Identifier;

                if (!_scope.TryGetDeclaration(in identifier, out _, out DeclareStatement declare))
                {
                    throw new InvalidOperationException($"Declaration of {identifier} is not found");
                }

                declare.Type.Binding = DefineReturnValueSchema();
            }
        }
        protected virtual List<ColumnExpression> DefineReturnValueSchema()
        {
            return new List<ColumnExpression>()
            {
                new()
                {
                    Alias = "Code",
                    Expression = new ScalarExpression() { Token = TokenType.String }
                },
                new()
                {
                    Alias = "Value",
                    Expression = new ScalarExpression() { Token = TokenType.String }
                }
            };
        }
        private void ConfigureProcessorOptions()
        {
            if (_statement.Options is null) { return; }

            _options.TrimExcess(_statement.Options.Count);

            foreach (ColumnExpression option in _statement.Options)
            {
                if (StreamFactory.TryEvaluate(in _scope, option.Expression, out object value))
                {
                    _options.Add(option.Alias, value);
                }
                else
                {
                    _options.Add(option.Alias, null);
                }
            }
        }
    }
}