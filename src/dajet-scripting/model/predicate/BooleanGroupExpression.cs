namespace DaJet.Scripting.Model
{
    public sealed class BooleanGroupExpression : SyntaxNode
    {
        public BooleanGroupExpression()
        {
            Token = TokenType.OpenRoundBracket;
        }
        public SyntaxNode Expression { get; set; }
    }
}