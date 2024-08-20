namespace DaJet.Scripting.Model
{
    public sealed class ContinueStatement : SyntaxNode
    {
        public ContinueStatement() { Token = TokenType.CONTINUE; }
    }
}