namespace DaJet.Scripting.Model
{
    public sealed class DeclareStatement : SyntaxNode
    {
        public DeclareStatement() { Token = TokenType.DECLARE; }
        public string Name { get; set; } = string.Empty;
        public TypeIdentifier Type { get; set; }
        public TypeReference TypeOf { get; set; }
        public SyntaxNode Initializer { get; set; }
        public override string ToString()
        {
            return $"[{Token}: {Name}]";
        }
    }
}