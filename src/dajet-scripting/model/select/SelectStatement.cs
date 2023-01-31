namespace DaJet.Scripting.Model
{
    public sealed class SelectStatement : SyntaxNode
    {
        public SelectStatement() { Token = TokenType.SELECT; }
        public SyntaxNode Select { get; set; }
        public CommonTableExpression CommonTables { get; set; }
    }
}