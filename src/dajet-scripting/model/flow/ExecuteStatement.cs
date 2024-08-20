namespace DaJet.Scripting.Model
{
    public sealed class ExecuteStatement : SyntaxNode
    {
        public ExecuteStatement() { Token = TokenType.EXECUTE; }
    }
}