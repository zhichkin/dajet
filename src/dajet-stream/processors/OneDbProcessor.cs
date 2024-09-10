using DaJet.Data;
using DaJet.Scripting;
using DaJet.Scripting.Model;

namespace DaJet.Stream
{
    public abstract class OneDbProcessor : IProcessor
    {
        protected IProcessor _next;
        protected readonly StreamScope _scope;
        protected Uri _uri;
        protected VariableReference _into;
        protected SqlStatement _statement;
        protected readonly int _yearOffset;
        protected readonly IDbConnectionFactory _factory;
        protected readonly Dictionary<string, object> _parameters = new();
        protected readonly Dictionary<string, string> _variables = new();
        protected readonly Dictionary<string, FunctionExpression> _functions = new();
        public OneDbProcessor(in StreamScope scope)
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

            StreamFactory.ConfigureFunctionsMap(in _scope, in _functions);

            _ = StreamFactory.TryGetIntoVariable(_statement.Node, out _into);

            foreach (var map in _variables)
            {
                if (!_parameters.ContainsKey(map.Value))
                {
                    _parameters.Add(map.Value, null);
                }
            }

            foreach (var map in _functions)
            {
                if (!_parameters.ContainsKey(map.Key))
                {
                    _parameters.Add(map.Key, null);
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

            foreach (var map in _functions)
            {
                object result = StreamFactory.InvokeFunction(in _scope, map.Value);

                _parameters[map.Key] = result ?? string.Empty;
            }
        }
    }
}