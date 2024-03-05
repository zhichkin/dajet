using DaJet.Metadata;
using DaJet.Scripting.Model;

namespace DaJet.Stream
{
    public sealed class UseProcessor : IProcessor
    {
        private IProcessor _next;
        private readonly StreamScope _scope;
        private readonly IProcessor _stream;
        private IMetadataProvider _database;
        private readonly string _uri;

        public UseProcessor(in StreamScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not UseStatement statement)
            {
                throw new ArgumentException(nameof(UseStatement));
            }

            _uri = statement.Uri;

            Uri uri = _scope.GetUri(in _uri);

            _database = MetadataService.CreateOneDbMetadataProvider(in uri);

            StreamFactory.InitializeVariables(in _scope, in _database);

            _stream = StreamFactory.CreateStream(in _scope);
        }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Synchronize() { _next?.Synchronize(); }
        public void Dispose() { _next?.Dispose(); }
        public void Process()
        {
            _stream?.Process();
            
            _next?.Process();
        }
    }
}