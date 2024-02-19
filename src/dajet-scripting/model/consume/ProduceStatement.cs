namespace DaJet.Scripting.Model
{
    public sealed class ProduceStatement : SyntaxNode
    {
        // PRODUCE <uri> WITH <options> SELECT <payload>
        public ProduceStatement() { Token = TokenType.PRODUCE; }
        public string Target { get; set; } // uri template string
        public List<ColumnExpression> Options { get; set; } = new(); // WITH clause
        public List<ColumnExpression> Columns { get; set; } = new(); // SELECT clause
    }
}