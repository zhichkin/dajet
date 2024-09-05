namespace DaJet.Scripting.Model
{
    public sealed class ForEachStatement : SyntaxNode
    {
        public ForEachStatement() { Token = TokenType.FOR; }
        public VariableReference Variable { get; set; }
        public VariableReference Iterator { get; set; }
        public int DegreeOfParallelism { get; set; } = 1;
        public StatementBlock Statements { get; set; } = new();
        public override string ToString()
        {
            return $"FOR EACH {Variable} IN {Iterator} MAXDOP {DegreeOfParallelism}";
        }
    }
}