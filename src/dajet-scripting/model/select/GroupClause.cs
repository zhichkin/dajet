namespace DaJet.Scripting.Model
{
    public sealed class GroupClause : SyntaxNode
    {
        public GroupClause()
        {
            Token = TokenType.GROUP;
        }
        public List<SyntaxNode> Expressions { get; set; } = new();
    }
}