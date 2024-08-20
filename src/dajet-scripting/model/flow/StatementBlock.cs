namespace DaJet.Scripting.Model
{
    public sealed class StatementBlock : SyntaxNode
    {
        public StatementBlock() { Token = TokenType.BEGIN; }
        public List<SyntaxNode> Statements { get; set; } = new();
    }
}