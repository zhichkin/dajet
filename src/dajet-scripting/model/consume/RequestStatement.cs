namespace DaJet.Scripting.Model
{
    public sealed class RequestStatement : SyntaxNode
    {
        // REQUEST <uri> [WITH <headers>] SELECT <options> INTO <response>
        public RequestStatement() { Token = TokenType.REQUEST; }
        public string Target { get; set; } // uri template string
        public VariableReference Response { get; set; } // INTO clause
        public List<ColumnExpression> Headers { get; set; } = new(); // WITH clause
        public List<ColumnExpression> Options { get; set; } = new(); // SELECT clause
    }
}