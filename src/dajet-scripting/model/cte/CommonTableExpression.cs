namespace DaJet.Scripting.Model
{
    public sealed class CommonTableExpression : SyntaxNode
    {
        public CommonTableExpression()
        {
            Token = TokenType.CTE;
        }
        public string Name { get; set; } = string.Empty;
        public SyntaxNode Expression { get; set; } = null!;
        public List<Identifier> Columns { get; set; } = new();
        public CommonTableExpression Next { get; set; } = null!;
    }
}