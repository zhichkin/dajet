namespace DaJet.Scripting.Model
{
    public sealed class TypeIdentifier : SyntaxNode
    {
        public TypeIdentifier() { Token = TokenType.Type; }
        public object Tag { get; set; }
        public string Identifier { get; set; } = string.Empty;
        public override string ToString()
        {
            return $"[{Token}:{Identifier}]";
        }
    }
}