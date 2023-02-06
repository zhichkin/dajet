namespace DaJet.Scripting.Model
{
    public sealed class OverClause : SyntaxNode
    {
        public OverClause() { Token = TokenType.OVER; }
        public PartitionClause Partition { get; set; } = new(); // optional
        public OrderClause Order { get; set; } // optional
        public WindowFrame Preceding { get; set; } // optional
        public WindowFrame Following { get; set; } // optional
        public TokenType FrameType { get; set; } = TokenType.ROWS; // ROWS | RANGE
    }
}