namespace DaJet.Scripting.Model
{
    public sealed class TypeDefinition : SyntaxNode
    {
        public TypeDefinition() { Token = TokenType.TYPE; }
        public string Identifier { get; set; } = string.Empty;
        public List<PropertyDefinition> Properties { get; } = new();
        public override string ToString()
        {
            return $"[{Token}: {Identifier}]";
        }
    }
}