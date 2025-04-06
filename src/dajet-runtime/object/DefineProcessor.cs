using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class DefineProcessor : IProcessor
    {
        private IProcessor _next;
        private readonly ScriptScope _scope;
        private readonly TypeDefinition _statement;
        public DefineProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not TypeDefinition statement)
            {
                throw new ArgumentException(nameof(TypeDefinition));
            }

            _statement = statement;

            if (!StreamFactory.TypeRegistry.TryAdd(_statement.Identifier, _statement))
            {
                FileLogger.Default.Write($"Duplicate type definition: [{_statement.Identifier}]");
            }
        }
        public void Dispose() { _next?.Dispose(); }
        public void Process() { _next?.Process(); }
        public void Synchronize() { _next?.Synchronize(); }
        public void LinkTo(in IProcessor next) { _next = next; }
    }
}