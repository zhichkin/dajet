namespace DaJet.Scripting.Model
{
    public sealed class OutputClause : SyntaxNode
    {
        public OutputClause() { Token = TokenType.OUTPUT; }
        public List<SyntaxNode> Expressions { get; set; } = new();
    }
}