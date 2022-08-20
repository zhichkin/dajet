namespace DaJet.Scripting.Model
{
    public sealed class DeclareStatement : SyntaxNode
    {
        public DeclareStatement()
        {
            Token = TokenType.DECLARE;
        }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public SyntaxNode Initializer { get; set; } = null!;
    }
}