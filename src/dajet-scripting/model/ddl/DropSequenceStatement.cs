namespace DaJet.Scripting.Model
{
    public sealed class DropSequenceStatement : SyntaxNode
    {
        public DropSequenceStatement() { Token = TokenType.SEQUENCE; }
        public string Identifier { get; set; }
    }
}