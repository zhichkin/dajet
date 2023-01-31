namespace DaJet.Scripting.Model
{
    public sealed class VariableReference : SyntaxNode
    {
        public VariableReference() { Token = TokenType.Variable; }
        public object Tag { get; set; }
        public string Identifier { get; set; } = string.Empty;
    }
}