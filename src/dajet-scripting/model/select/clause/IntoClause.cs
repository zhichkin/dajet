namespace DaJet.Scripting.Model
{
    public sealed class IntoClause : SyntaxNode
    {
        public IntoClause() { Token = TokenType.INTO; }
        public TableReference Table { get; set; }
        public VariableReference Value { get; set; }
        public List<ColumnExpression> Columns { get; set; }
    }
}