namespace DaJet.Scripting.Model
{
    public sealed class CreateTypeStatement : SyntaxNode
    {
        public CreateTypeStatement() { Token = TokenType.TYPE; }
        public string Identifier { get; set; } = string.Empty;
        public List<ColumnDefinition> Columns { get; } = new();
    }
}