namespace DaJet.Scripting.Model
{
    public sealed class RequestStatement : SyntaxNode
    {
        // REQUEST <uri> [WHEN <condition>] [WITH <headers>] [SELECT <options>] INTO <response>
        public RequestStatement() { Token = TokenType.REQUEST; }
        public string Target { get; set; } // uri template string
        public SyntaxNode When { get; set; } // WHEN clause
        public VariableReference Response { get; set; } // INTO clause
        public List<ColumnExpression> Headers { get; set; } = new(); // WITH clause
        public List<ColumnExpression> Options { get; set; } = new(); // SELECT clause
    }
}