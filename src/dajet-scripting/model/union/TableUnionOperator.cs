namespace DaJet.Scripting.Model
{
    public sealed class TableUnionOperator : SyntaxNode
    {
        public TableUnionOperator()
        {
            Token = TokenType.UNION;
        }
        public SyntaxNode Expression1 { get; set; } = null!;
        public SyntaxNode Expression2 { get; set; } = null!;
    }
}