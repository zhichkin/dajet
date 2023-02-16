namespace DaJet.Scripting.Model
{
    public sealed class InsertStatement : SyntaxNode
    {
        public InsertStatement() { Token = TokenType.INSERT; }
        public CommonTableExpression CommonTables { get; set; }
        public TableReference Target { get; set; }
        public List<ColumnReference> Columns { get; set; } = new();
        public SyntaxNode Source { get; set; }
    }
}