namespace DaJet.Scripting.Model
{
    public sealed class ComparisonOperator : SyntaxNode
    {
        public SyntaxNode Expression1 { get; set; } = null!;
        public SyntaxNode Expression2 { get; set; } = null!;
    }
}