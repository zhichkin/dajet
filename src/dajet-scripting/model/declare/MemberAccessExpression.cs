namespace DaJet.Scripting.Model
{
    public sealed class MemberAccessExpression : SyntaxNode
    {
        public MemberAccessExpression() { Token = TokenType.Variable; }
        public string Identifier { get; set; } = string.Empty;
        public object Binding { get; set; }
        public override string ToString()
        {
            return $"[{Token}: {Identifier}]";
        }
    }
}