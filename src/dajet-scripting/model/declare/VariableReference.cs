namespace DaJet.Scripting.Model
{
    public sealed class VariableReference : SyntaxNode
    {
        public VariableReference() { Token = TokenType.Variable; }
        public string Identifier { get; set; } = string.Empty;
        public object Binding { get; set; }
        public override string ToString()
        {
            return $"[{Token}: {Identifier}]";
        }
    }
}