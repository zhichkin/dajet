using DaJet.Metadata;
using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class UseProcessor : IProcessor
    {
        private IProcessor _next;
        private IProcessor _block;
        private readonly ScriptScope _scope;
        private readonly UseStatement _statement;
        public UseProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not UseStatement statement)
            {
                throw new ArgumentException(nameof(UseStatement));
            }

            _statement = statement;
        }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Process()
        {
            InitializeProcessor();
            _block.Process();
            _next?.Process();
        }
        public void Synchronize()
        {
            _block.Synchronize();
            _next?.Synchronize();
        }
        public void Dispose()
        {
            _block.Dispose();
            _next?.Dispose();
        }
        private void InitializeProcessor()
        {
            if (_block is not null)
            {
                return; // lazy initialization
            }

            if (!_scope.TryGetMetadataProvider(out IMetadataProvider database, out string error))
            {
                throw new InvalidOperationException(error);
            }

            ScriptScope block_scope = _scope.Create(_statement.Statements);

            StreamFactory.InitializeVariables(in block_scope, in database);

            _block = StreamFactory.CreateStream(in block_scope);
        }
    }
}