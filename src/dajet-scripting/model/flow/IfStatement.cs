namespace DaJet.Scripting.Model
{
    public sealed class IfStatement : SyntaxNode
    {
        public IfStatement() { Token = TokenType.IF; }
        public SyntaxNode IF { get; set; }
        public StatementBlock THEN { get; set; } = new();
        public StatementBlock ELSE { get; set; } = new();
    }
}