namespace DaJet.Scripting.Model
{
    public sealed class UseStatement : SyntaxNode
    {
        public UseStatement() { Token = TokenType.USE; }
        public string Uri { get; set; } // uri template string
        public override string ToString()
        {
            return $"{Uri}";
        }
    }
}