namespace DaJet.Scripting.Model
{
    public sealed class DeleteStatement : SyntaxNode
    {
        public DeleteStatement() { Token = TokenType.DELETE; }
        public CommonTableExpression CTE { get; set; } = null!;
        public TableSource TARGET { get; set; } = null!;
        public OutputClause OUTPUT { get; set; } = null!;
        public FromClause FROM { get; set; } = null!; // (ms) JOIN or (pg) USING
        public WhereClause WHERE { get; set; } = null!;
    }
}