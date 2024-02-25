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
        private readonly StreamContext _context;
        private readonly IMetadataProvider _database;

        public UseProcessor(in ScriptScope scope, in StreamContext context)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not UseStatement statement)
            {
                throw new InvalidOperationException();
            }

            _context = context ?? throw new ArgumentNullException(nameof(context));

            _database = StreamProcessor.GetDatabaseContext(in statement);

            foreach (var item in _scope.Variables)
            {
                if (item.Value is DeclareStatement declare)
                {
                    //TODO: configure declare variable
                }
            }

            _stream = StreamFactory.Create(_scope.Children, in _context);

            //TODO: _context.MapUri(statement.Uri.ToString());
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