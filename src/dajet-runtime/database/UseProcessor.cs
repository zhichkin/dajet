using DaJet.Metadata;
using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class UseProcessor : IProcessor
    {
        private IProcessor _next;
        private IProcessor _body;
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
            _body?.Process();
            _next?.Process();
        }
        public void Synchronize()
        {
            _body?.Synchronize();
            _next?.Synchronize();
        }
        public void Dispose()
        {
            _body?.Dispose();
            _next?.Dispose();
        }
        private void InitializeProcessor()
        {
            if (_body is not null)
            {
                return; // lazy initialization
            }

            if (!_scope.TryGetMetadataProvider(out IMetadataProvider database, out string error))
            {
                throw new InvalidOperationException(error);
            }

            ScriptScope body_scope = _scope.Create(_statement.Statements);

            StreamFactory.InitializeVariables(in body_scope, in database);

            _body = StreamFactory.CreateStream(in body_scope);
        }
    }
}