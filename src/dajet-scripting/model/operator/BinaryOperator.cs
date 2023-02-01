namespace DaJet.Scripting.Model
{
    public sealed class BinaryOperator : SyntaxNode
    {
        public SyntaxNode Expression1 { get; set; }
        public SyntaxNode Expression2 { get; set; }
    }
}