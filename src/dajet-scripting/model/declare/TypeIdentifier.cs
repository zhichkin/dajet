namespace DaJet.Scripting.Model
{
    public sealed class TypeIdentifier : SyntaxNode
    {
        public TypeIdentifier() { Token = TokenType.Type; }
        public string Identifier { get; set; } = string.Empty;
        public object Binding { get; set; }
        public override string ToString()
        {
            return $"[{Token}:{Identifier}]";
        }
    }
}