namespace DaJet.Scripting.Model
{
    public sealed class ProcessStatement : SyntaxNode
    {
        // PROCESS <variable> [,...n] WITH <processor> [INTO <variable>] [SELECT <options>]
        public ProcessStatement() { Token = TokenType.PROCESS; }
        public string Processor { get; set; } // WITH clause, .NET class identifier
        public List<VariableReference> Variables { get; set; } = new(); // list of variables
        public VariableReference Return { get; set; } // INTO clause
        public List<ColumnExpression> Options { get; set; } = new(); // SELECT clause
    }
}