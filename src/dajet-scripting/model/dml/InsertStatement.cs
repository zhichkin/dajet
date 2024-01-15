namespace DaJet.Scripting.Model
{
    public sealed class InsertStatement : SyntaxNode
    {
        public InsertStatement() { Token = TokenType.INSERT; }
        public CommonTableExpression CommonTables { get; set; }
        public TableReference Target { get; set; }
        public SyntaxNode Source { get; set; }
        ///<summary>OUTPUT clause is not implemented (parser and sql transpiler)</summary>
        public OutputClause Output { get; set; }
    }
}