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

            _stream = StreamFactory.CreateStream(in _scope);
        }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Synchronize() { _next?.Synchronize(); }
        public void Dispose() { _next?.Dispose(); }
        public void Process()
        {
            if (_database is null) // lazy initialization
            {
                _database = GetDatabaseContext();

                StreamFactory.InitializeVariables(in _scope, in _database);
            }

            _stream?.Process();
            
            _next?.Process();
        }
        private IMetadataProvider GetDatabaseContext()
        {
            Uri uri = _scope.GetUri(in _uri);

            return StreamManager.GetDatabaseContext(in uri);
        }
    }
}