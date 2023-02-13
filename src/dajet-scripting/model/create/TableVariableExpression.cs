namespace DaJet.Scripting.Model
{
    public sealed class TableVariableExpression : SyntaxNode
    {
        public TableVariableExpression() { Token = TokenType.Table; }
        public string Name { get; set; } = string.Empty;
        public SyntaxNode Expression { get; set; }
    }
}