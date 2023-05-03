namespace DaJet.Scripting.Model
{
    public sealed class ConsumeStatement : SyntaxNode
    {
        public ConsumeStatement() { Token = TokenType.CONSUME; }
        public List<ColumnExpression> Columns { get; set; } = new();
        public TopClause Top { get; set; }
        public FromClause From { get; set; }
        public WhereClause Where { get; set; }
        public OrderClause Order { get; set; }
    }
}