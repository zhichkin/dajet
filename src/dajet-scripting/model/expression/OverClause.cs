namespace DaJet.Scripting.Model
{
    public sealed class OverClause : SyntaxNode
    {
        public OverClause() { Token = TokenType.OVER; }
        public List<SyntaxNode> Partition { get; set; } = new(); // optional
        public OrderClause Order { get; set; } = null!; // optional
        public TokenType FrameType { get; set; } = TokenType.ROWS; // ROWS | RANGE
        public WindowFrame Preceding { get; set; } = null!; // optional
        public WindowFrame Following { get; set; } = null!; // optional
    }
    public sealed class WindowFrame : SyntaxNode
    {
        public WindowFrame() { Token = TokenType.PRECEDING; } // PRECEDING | FOLLOWING
        public int Extent { get; set; } = -1; // UNBOUNDED = -1, CURRENT ROW = 0
    }
}