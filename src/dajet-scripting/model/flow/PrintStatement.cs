namespace DaJet.Scripting.Model
{
    public sealed class PrintStatement : SyntaxNode
    {
        public PrintStatement() { Token = TokenType.PRINT; }
        public SyntaxNode Expression { get; set; }
    }
}