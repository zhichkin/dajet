namespace DaJet.Scripting.Model
{
    public sealed class FromClause : SyntaxNode
    {
        public FromClause() { Token = TokenType.FROM; }
        public SyntaxNode Expression { get; set; }
    }
}