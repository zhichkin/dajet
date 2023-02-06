namespace DaJet.Scripting.Model
{
    public sealed class PartitionClause : SyntaxNode
    {
        public PartitionClause() { Token = TokenType.PARTITION; }
        public List<SyntaxNode> Columns { get; set; } = new();
    }
}