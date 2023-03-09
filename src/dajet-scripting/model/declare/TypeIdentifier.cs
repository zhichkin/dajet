namespace DaJet.Scripting.Model
{
    public sealed class TypeIdentifier : SyntaxNode
    {
        public TypeIdentifier() { Token = TokenType.Type; }
        public string Identifier { get; set; } = string.Empty;
        public int Qualifier1 { get; set; } = 0;
        public int Qualifier2 { get; set; } = 0;
        public object Binding { get; set; }
        public override string ToString()
        {
            return $"[{Token}:{Identifier}]";
        }
    }
}