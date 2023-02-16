namespace DaJet.Scripting.Model
{
    public sealed class ValuesExpression : SyntaxNode
    {
        public ValuesExpression() { Token = TokenType.VALUES; }
        public List<SyntaxNode> Values { get; set; } = new();
    }
}