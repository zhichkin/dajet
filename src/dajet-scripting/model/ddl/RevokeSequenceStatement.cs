namespace DaJet.Scripting.Model
{
    public sealed class RevokeSequenceStatement : SyntaxNode
    {
        // REVOKE SEQUENCE <sequence> ON <table>
        public RevokeSequenceStatement() { Token = TokenType.SEQUENCE; }
        public string Identifier { get; set; }
        public TableReference Table { get; set; }
    }
}