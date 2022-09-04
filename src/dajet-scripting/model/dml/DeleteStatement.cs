namespace DaJet.Scripting.Model
{
    public sealed class DeleteStatement : SyntaxNode
    {
        public DeleteStatement() { Token = TokenType.DELETE; }
        public CommonTableExpression CTE { get; set; } = null!;
        public FromClause FROM { get; set; } = null!;
        public OutputClause OUTPUT { get; set; } = null!;
        public WhereClause WHERE { get; set; } = null!;
    }
}