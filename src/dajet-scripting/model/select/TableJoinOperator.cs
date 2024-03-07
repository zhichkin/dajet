namespace DaJet.Scripting.Model
{
    public sealed class TableJoinOperator : SyntaxNode
    {
        public TableJoinOperator() { Token = TokenType.JOIN; }
        public OnClause On { get; set; }
        public SyntaxNode Expression1 { get; set; }
        public SyntaxNode Expression2 { get; set; }
        public TokenType Modifier { get; set; } = TokenType.Array; // APPEND operator
    }
}