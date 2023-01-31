namespace DaJet.Scripting.Model
{
    public sealed class OrderClause : SyntaxNode
    {
        public OrderClause() { Token = TokenType.ORDER; }
        public List<OrderExpression> Expressions { get; set; } = new();
        public SyntaxNode Offset { get; set; }
        public SyntaxNode Fetch { get; set; }
    }
    public sealed class OrderExpression : SyntaxNode
    {
        public OrderExpression() { Token = TokenType.ASC; }
        public SyntaxNode Expression { get; set; }
    }
}