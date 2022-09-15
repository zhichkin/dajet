namespace DaJet.Scripting.Model
{
    public sealed class CaseExpression : SyntaxNode
    {
        public CaseExpression() { Token = TokenType.CASE; }
        public List<SyntaxNode> CASE { get; set; } = new();
        public SyntaxNode ELSE { get; set; } = null!;
        public string Alias { get; set; } = string.Empty;
    }
    public sealed class WhenExpression : SyntaxNode
    {
        public WhenExpression() { Token = TokenType.WHEN; }
        public SyntaxNode WHEN { get; set; } = null!;
        public SyntaxNode THEN { get; set; } = null!;
    }
}