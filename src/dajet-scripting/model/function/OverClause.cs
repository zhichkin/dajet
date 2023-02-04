namespace DaJet.Scripting.Model
{
    public sealed class OverClause : SyntaxNode
    {
        public OverClause() { Token = TokenType.OVER; }
        public PartitionClause Partition { get; set; } = new(); // optional
        public OrderClause Order { get; set; } // optional
        public TokenType FrameType { get; set; } = TokenType.ROWS; // ROWS | RANGE
        public WindowFrame Preceding { get; set; } // optional
        public WindowFrame Following { get; set; } // optional
    }
    public sealed class PartitionClause : SyntaxNode
    {
        public PartitionClause() { Token = TokenType.PARTITION; }
        public List<SyntaxNode> Columns { get; set; } = new();
    }
    public sealed class WindowFrame : SyntaxNode
    {
        public WindowFrame() { Token = TokenType.PRECEDING; } // PRECEDING | FOLLOWING
        public int Extent { get; set; } = -1; // UNBOUNDED = -1, CURRENT ROW = 0
    }
}