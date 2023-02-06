namespace DaJet.Scripting.Model
{
    public sealed class WhenClause : SyntaxNode
    {
        public WhenClause() { Token = TokenType.WHEN; }
        public SyntaxNode WHEN { get; set; }
        public SyntaxNode THEN { get; set; }
    }
}