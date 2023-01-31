namespace DaJet.Scripting.Model
{
    public sealed class HavingClause : SyntaxNode
    {
        public HavingClause() { Token = TokenType.HAVING; }
        public SyntaxNode Expression { get; set; }
    }
}