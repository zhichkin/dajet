using DaJet.Metadata;
using DaJet.Scripting;
using DaJet.Scripting.Model;

namespace DaJet.Stream
{
    public sealed class UseProcessor : IProcessor
    {
        private IProcessor _next;
        private readonly ScriptScope _scope;
        private readonly IProcessor _stream;
        private IMetadataProvider _database;
        private readonly string _uri;
        private readonly string[] _uri_templates;

        public UseProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not UseStatement statement)
            {
                throw new ArgumentException(nameof(UseStatement));
            }

            _uri = statement.Uri;

            _uri_templates = StreamScope.GetUriVariables(in _uri);

            _stream = StreamFactory.CreateStream(in _scope);
        }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Synchronize() { _next?.Synchronize(); }
        public void Dispose() { _next?.Dispose(); }
        public void Process()
        {
            _database ??= GetDatabaseContext();

            StreamProcessor.InitializeVariables(in _scope, in _database);

            _stream?.Process();
            
            _next?.Process();
        }
        private IMetadataProvider GetDatabaseContext()
        {
            if (_uri_templates.Length == 0)
            {
                return StreamProcessor.GetDatabaseContext(new Uri(_uri));
            }

            Dictionary<string, string> values = new(_uri_templates.Length);

            for (int i = 0; i < _uri_templates.Length; i++)
            {
                string variable = _uri_templates[i].TrimStart('{').TrimEnd('}');

                if (_scope.TryGetValue(in variable, out object value))
                {
                    values.Add(_uri_templates[i], value.ToString());
                }
                else
                {
                    values.Add(_uri_templates[i], string.Empty);
                }
            }

            Uri uri = StreamScope.GetUri(in _uri, in values); //TODO: encapsulate !!!

            return StreamProcessor.GetDatabaseContext(in uri);
        }
    }
}