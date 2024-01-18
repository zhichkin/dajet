namespace DaJet.Scripting.Model
{
    public sealed class ComparisonOperator : SyntaxNode
    {
        public TokenType Modifier { get; set; }
        public SyntaxNode Expression1 { get; set; }
        public SyntaxNode Expression2 { get; set; }
    }
}