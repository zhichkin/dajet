namespace DaJet.Scripting.Model
{
    public sealed class CommonTableExpression : SyntaxNode
    {
        public CommonTableExpression() { Token = TokenType.Table; }
        public string Name { get; set; } = string.Empty;
        public SyntaxNode Expression { get; set; }
        public CommonTableExpression Next { get; set; }
    }
}