namespace DaJet.Scripting.Model
{
    public sealed class StarExpression : SyntaxNode
    {
        public StarExpression() { Token = TokenType.Star; }
    }
}