namespace DaJet.Scripting.Model
{
    public sealed class SelectStatement : SyntaxNode
    {
        public CommonTableExpression CTE { get; set; } = null!;
        public List<SyntaxNode> SELECT { get; set; } = new();
        public SyntaxNode TOP { get; set; } = null!;
        public FromClause FROM { get; set; } = null!;
        public WhereClause WHERE { get; set; } = null!;
        public GroupClause GROUP { get; set; } = null!;
        public HavingClause HAVING { get; set; } = null!;
        public OrderClause ORDER { get; set; } = null!;
    }
}