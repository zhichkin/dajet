using DaJet.Data;
using DaJet.Scripting.Model;
using DaJet.Scripting;

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
        protected readonly Dictionary<string, string> _variables = new();
        protected readonly Dictionary<string, string> _functions = new();
        protected readonly Dictionary<string, object> _parameters = new();
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
        public void Synchronize() { _next?.Synchronize(); }
        public void Dispose() { _next?.Dispose(); }
        public abstract void Process();
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
                if (!_parameters.ContainsKey(map.Value))
                {
                    _parameters.Add(map.Value, null);
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
                if (_scope.TryGetValue(map.Key, out object value))
                {
                    if (value is DataObject record)
                    {
                        _parameters[map.Value] = StreamScope.ToJson(in record);
                    }
                    else if (value is List<DataObject> table)
                    {
                        _parameters[map.Value] = StreamScope.ToJson(in table);
                    }
                    else
                    {
                        _parameters[map.Value] = string.Empty;
                    }
                }
            }
        }
    }
}