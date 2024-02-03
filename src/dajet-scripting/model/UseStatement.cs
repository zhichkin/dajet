namespace DaJet.Scripting.Model
{
    public sealed class UseStatement : SyntaxNode
    {
        public UseStatement() { Token = TokenType.USE; }
        public Uri Uri { get; set; }
        public override string ToString()
        {
            return $"{Uri}";
        }
    }
}