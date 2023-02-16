namespace DaJet.Scripting.Model
{
    public sealed class DeleteStatement : SyntaxNode
    {
        public DeleteStatement() { Token = TokenType.DELETE; }
        public CommonTableExpression CommonTables { get; set; }
        public TableSource TARGET { get; set; }
        public OutputClause OUTPUT { get; set; }
        public FromClause FROM { get; set; } // (ms) JOIN or (pg) USING
        public WhereClause WHERE { get; set; }
    }
}