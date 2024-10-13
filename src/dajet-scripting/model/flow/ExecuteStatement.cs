namespace DaJet.Scripting.Model
{
    public sealed class ExecuteStatement : SyntaxNode
    {
        // EXECUTE 'file://script.djs' [WITH <parameters>] [INTO <variable>]
        public ExecuteStatement() { Token = TokenType.EXECUTE; }
        public string Uri { get; set; } // script uri template
        public List<ColumnExpression> Parameters { get; set; } = new(); // WITH clause
        public VariableReference Return { get; set; } // INTO clause
        public override string ToString()
        {
            return $"EXECUTE {Uri}";
        }
    }
}