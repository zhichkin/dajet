namespace DaJet.Scripting.Model
{
    public sealed class ReturnStatement : SyntaxNode
    {
        // RETURN <expression>
        public ReturnStatement() { Token = TokenType.RETURN; }
        public SyntaxNode Expression { get; set; } //NOTE: required !!!
    }
}