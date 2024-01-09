namespace DaJet.Scripting.Model
{
    public sealed class SelectExpression : SyntaxNode
    {
        public SelectExpression() { Token = TokenType.SELECT; }
        public FromClause From { get; set; }
        // false = FROM first TableExpression or right JOIN operand
        // true = all other cases (correlated subquery)
        public bool IsCorrelated { get; set; } = true;
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