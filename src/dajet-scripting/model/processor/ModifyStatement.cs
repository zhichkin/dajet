namespace DaJet.Scripting.Model
{
    public sealed class ModifyStatement : SyntaxNode
    {
        public ModifyStatement() { Token = TokenType.MODIFY; }
        public VariableReference Target { get; set; }
        public VariableReference Source { get; set; } // FROM clause
        public List<ColumnReference> Delete { get; set; } = new(); // DELETE clause
        public List<ColumnExpression> Select { get; set; } = new(); // SELECT clause
    }
}