namespace DaJet.Scripting.Model
{
    public sealed class OnClause : SyntaxNode
    {
        public OnClause() { Token = TokenType.ON; }
        public SyntaxNode Expression { get; set; }
    }
}