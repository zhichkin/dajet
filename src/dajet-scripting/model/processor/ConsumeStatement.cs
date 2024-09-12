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
        public bool StrictOrderRequired { get; set; } // do not use hints (ms) READPAST or (pg) SKIP LOCKED
        public IntoClause Into { get; set; }

        // CONSUME <uri> WITH <options> INTO <variable> ... RabbitMQ and Apache Kafka
        public string Target { get; set; } // uri template string
        public List<ColumnExpression> Options { get; set; } = new(); // WITH clause
    }
}