namespace DaJet.Scripting.Model
{
    public sealed class SelectStatement : SyntaxNode
    {
        public SelectStatement() { Token = TokenType.SELECT; }
        public SyntaxNode Expression { get; set; }
        public CommonTableExpression CommonTables { get; set; }
        public bool IsStream { get; set; } // STREAM statement
    }
}