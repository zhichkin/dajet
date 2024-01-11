namespace DaJet.Scripting.Model
{
    public sealed class OrderExpression : SyntaxNode
    {
        public OrderExpression() { Token = TokenType.ASC; }
        public SyntaxNode Expression { get; set; }
    }
}