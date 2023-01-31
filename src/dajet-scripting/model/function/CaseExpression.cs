namespace DaJet.Scripting.Model
{
    public sealed class CaseExpression : SyntaxNode
    {
        public CaseExpression() { Token = TokenType.CASE; }
        public string Alias { get; set; } = string.Empty;
        public List<WhenExpression> CASE { get; set; } = new();
        public SyntaxNode ELSE { get; set; }
    }
    public sealed class WhenExpression : SyntaxNode
    {
        public WhenExpression() { Token = TokenType.WHEN; }
        public SyntaxNode WHEN { get; set; }
        public SyntaxNode THEN { get; set; }
    }
}