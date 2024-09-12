namespace DaJet.Scripting.Model
{
    public sealed class ImportStatement : SyntaxNode
    {
        public ImportStatement() { Token = TokenType.IMPORT; }
        public string Source { get; set; } = string.Empty;
        public List<VariableReference> Target { get; set; } = new();
    }
}