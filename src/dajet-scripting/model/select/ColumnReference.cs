namespace DaJet.Scripting.Model
{
    public sealed class ColumnReference : SyntaxNode
    {
        public ColumnReference() { Token = TokenType.Column; }
        public object Tag { get; set; }
        public string Identifier { get; set; } = string.Empty;
        public override string ToString()
        {
            return $"[{Token}:{Identifier}]";
        }
    }
}