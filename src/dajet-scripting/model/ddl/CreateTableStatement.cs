namespace DaJet.Scripting.Model
{
    public sealed class CreateTableStatement : SyntaxNode
    {
        public CreateTableStatement() { Token = TokenType.TABLE; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}