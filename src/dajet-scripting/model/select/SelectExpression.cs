namespace DaJet.Scripting.Model
{
    public sealed class SelectExpression : SyntaxNode
    {
        public SelectExpression() { Token = TokenType.SELECT; }
        public FromClause From { get; set; }
        public bool IsCorrelated { get; set; } // true = correlated subquery, false = all other cases
        public List<ColumnExpression> Columns { get; set; } = new();
        public bool Distinct { get; set; }
        public TopClause Top { get; set; }
        public WhereClause Where { get; set; }
        public GroupClause Group { get; set; }
        public HavingClause Having { get; set; }
        public OrderClause Order { get; set; }
        // PG hack = FOR UPDATE SKIP LOCKED
        // MS hack = WITH (ROWLOCK, READPAST)
        public string Hints { get; set; }
        public IntoClause Into { get; set; }
    }
}