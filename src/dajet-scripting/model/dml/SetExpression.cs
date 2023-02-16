namespace DaJet.Scripting.Model
{
    public sealed class SetExpression : SyntaxNode
    {
        public SetExpression() { Token = TokenType.SET; }
        public ColumnReference Column { get; set; }
        public SyntaxNode Initializer { get; set; }
    }
}