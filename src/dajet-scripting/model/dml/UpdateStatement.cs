namespace DaJet.Scripting.Model
{
    public sealed class UpdateStatement : SyntaxNode
    {
        public UpdateStatement() { Token = TokenType.UPDATE; }
        public CommonTableExpression CommonTables { get; set; }
        public TableReference Target { get; set; }
        public List<SetExpression> Set { get; set; } = new();
        public FromClause Source { get; set; }
        public WhereClause Where { get; set; }
    }
}