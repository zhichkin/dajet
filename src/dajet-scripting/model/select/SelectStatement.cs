namespace DaJet.Scripting.Model
{
    public sealed class SelectStatement : SyntaxNode
    {
        public CommonTableExpression CTE { get; set; } = null!;
        public List<SyntaxNode> SELECT { get; set; } = new();
        public FromClause FROM { get; set; } = null!;
        public WhereClause WHERE { get; set; } = null!;
    }
}