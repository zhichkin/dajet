namespace DaJet.Scripting.Model
{
    public sealed class TableExpression : SyntaxNode
    {
        public TableExpression() { Token = TokenType.Table; }
        public string Alias { get; set; } = string.Empty;
        public SyntaxNode Expression { get; set; }
    }
}