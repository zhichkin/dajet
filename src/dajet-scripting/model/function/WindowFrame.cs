namespace DaJet.Scripting.Model
{
    public sealed class WindowFrame : SyntaxNode
    {
        public WindowFrame() { Token = TokenType.PRECEDING; } // PRECEDING | FOLLOWING
        public int Extent { get; set; } = -1; // UNBOUNDED = -1, CURRENT ROW = 0
    }
}