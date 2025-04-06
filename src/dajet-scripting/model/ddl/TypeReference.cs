namespace DaJet.Scripting.Model
{
    public sealed class TypeReference : SyntaxNode
    {
        public TypeReference() { Token = TokenType.Type; }
        public string Identifier { get; set; } = string.Empty;
        public override string ToString()
        {
            return $"[{Token}: {Identifier}]";
        }
    }
}