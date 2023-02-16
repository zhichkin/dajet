namespace DaJet.Scripting.Model
{
    public sealed class TableSource : SyntaxNode
    {
        public TableSource() { Token = TokenType.Table; }
        public SyntaxNode Expression { get; set; }
        public List<TokenType> Hints { get; set; } = new();
    }
}