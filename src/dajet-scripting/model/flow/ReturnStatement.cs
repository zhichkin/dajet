namespace DaJet.Scripting.Model
{
    public sealed class ReturnStatement : SyntaxNode
    {
        public ReturnStatement() { Token = TokenType.RETURN; }
    }
}