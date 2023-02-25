namespace DaJet.Scripting.Model
{
    public sealed class UpsertStatement : SyntaxNode
    {
        public UpsertStatement() { Token = TokenType.UPSERT; }
        public CommonTableExpression CommonTables { get; set; }
        public bool IgnoreUpdate { get; set; }
        public TableReference Target { get; set; }
        public SyntaxNode Source { get; set; }
        public WhereClause Where { get; set; }
        public SetClause Set { get; set; } = new();
    }
}