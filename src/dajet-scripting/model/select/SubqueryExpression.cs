namespace DaJet.Scripting.Model
{
    public sealed class SubqueryExpression : SyntaxNode
    {
        public SubqueryExpression() { Token = TokenType.SELECT; }
        public string Alias { get; set; }
        public SyntaxNode Expression { get; set; }
    }
}