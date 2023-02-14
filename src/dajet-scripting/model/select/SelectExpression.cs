namespace DaJet.Scripting.Model
{
    public sealed class SelectExpression : SyntaxNode
    {
        public SelectExpression() { Token = TokenType.SELECT; }
        public bool Distinct { get; set; } = false;
        public List<ColumnExpression> Select { get; set; } = new();
        public TopClause Top { get; set; }
        public FromClause From { get; set; }
        public WhereClause Where { get; set; }
        public GroupClause Group { get; set; }
        public HavingClause Having { get; set; }
        public OrderClause Order { get; set; }
    }
}