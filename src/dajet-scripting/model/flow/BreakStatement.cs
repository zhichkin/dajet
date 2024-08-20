namespace DaJet.Scripting.Model
{
    public sealed class BreakStatement : SyntaxNode
    {
        public BreakStatement() { Token = TokenType.BREAK; }
    }
}