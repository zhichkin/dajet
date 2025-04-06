namespace DaJet.Scripting.Model
{
    public sealed class PropertyDefinition : SyntaxNode
    {
        public PropertyDefinition() { Token = TokenType.PROPERTY; }
        public string Name { get; set; } = string.Empty;
        public TypeIdentifier Type { get; set; }
        public override string ToString()
        {
            return $"[{Token}: {Name} {Type}]";
        }
    }
}