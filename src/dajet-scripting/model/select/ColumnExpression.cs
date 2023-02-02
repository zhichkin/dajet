namespace DaJet.Scripting.Model
{
    public sealed class ColumnExpression : SyntaxNode
    {
        public ColumnExpression() { Token = TokenType.Column; }
        public string Alias { get; set; } = string.Empty;
        public SyntaxNode Expression { get; set; }
    }
}