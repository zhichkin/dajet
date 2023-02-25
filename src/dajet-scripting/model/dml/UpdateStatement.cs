namespace DaJet.Scripting.Model
{
    public sealed class UpdateStatement : SyntaxNode
    {
        public UpdateStatement() { Token = TokenType.UPDATE; }
        public CommonTableExpression CommonTables { get; set; }
        public TableReference Target { get; set; }
        public SyntaxNode Source { get; set; }
        public WhereClause Where { get; set; }
        public SetClause Set { get; set; }
    }
}