namespace DaJet.Scripting.Model
{
    public class Identifier : SyntaxNode
    {
        public string Alias { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public object Tag { get; set; } = null!;
    }
}