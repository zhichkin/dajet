namespace DaJet.Scripting.Model
{
    public sealed class WhereClause : SyntaxNode
    {
        public WhereClause() { Token = TokenType.WHERE; }
        public SyntaxNode Expression { get; set; }
    }
}