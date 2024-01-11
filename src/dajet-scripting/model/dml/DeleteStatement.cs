namespace DaJet.Scripting.Model
{
    public sealed class DeleteStatement : SyntaxNode
    {
        public DeleteStatement() { Token = TokenType.DELETE; }
        public CommonTableExpression CommonTables { get; set; }
        public TableReference Target { get; set; }
        public WhereClause Where { get; set; }
        public OutputClause Output { get; set; }
        ///<summary>MS SQL Server (sql generator) and CONSUME statement context only</summary>
        public FromClause From { get; set; }
    }
}