namespace DaJet.Scripting.Model
{
    public sealed class FunctionExpression : SyntaxNode
    {
        public string Name { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;
        public List<SyntaxNode> Parameters { get; set; } = new();
        public OverClause Over { get; set; }
    }
}