namespace DaJet.Scripting.Model
{
    public sealed class TopClause : SyntaxNode
    {
        public TopClause() { Token = TokenType.TOP; }
        public SyntaxNode Expression { get; set; }
    }
}