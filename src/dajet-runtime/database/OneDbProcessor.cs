using DaJet.Data;
using DaJet.Scripting;
using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public abstract class OneDbProcessor : IProcessor
    {
        protected IProcessor _next;
        protected readonly ScriptScope _scope;
        protected Uri _uri;
        protected VariableReference _into;
        protected SqlStatement _statement;
        protected readonly int _yearOffset;
        protected readonly IDbConnectionFactory _factory;
        protected readonly Dictionary<string, object> _parameters = new();
        protected readonly Dictionary<string, string> _variables = new();
        protected readonly Dictionary<string, FunctionExpression> _functions = new();
        public OneDbProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            _uri = _scope.GetDatabaseUri();

            _factory = DbConnectionFactory.GetFactory(in _uri);

            _yearOffset = _factory.GetYearOffset(in _uri);

            _statement = StreamFactory.Transpile(in _scope);

            ConfigureParameters();
        }
        public void LinkTo(in IProcessor next) { _next = next; }
        public abstract void Process();
        public virtual void Synchronize() { _next?.Synchronize(); }
        public virtual void Dispose() { _next?.Dispose(); }
        private void ConfigureParameters()
        {
            StreamFactory.ConfigureVariablesMap(in _scope, in _variables);

            _ = StreamFactory.TryGetIntoVariable(_statement.Node, out _into);

            foreach (var map in _variables)
            {
                if (!_parameters.ContainsKey(map.Value))
                {
                    _parameters.Add(map.Value, null);
                }
            }

            foreach (FunctionDescriptor function in _statement.Functions)
            {
                if (!_parameters.ContainsKey(function.Target))
                {
                    _parameters.Add(function.Target, null);
                }
            }
        }
        protected void InitializeParameterValues()
        {
            foreach (var map in _variables)
            {
                if (_scope.TryGetValue(map.Key, out object value))
                {
                    _parameters[map.Value] = value;
                }
            }

            foreach (FunctionDescriptor function in _statement.Functions)
            {
                object result = StreamFactory.InvokeFunction(in _scope, function.Node);

                _parameters[function.Target] = result;
            }
        }
    }
}