namespace DaJet.Scripting.Model
{
    public sealed class CommentStatement : SyntaxNode
    {
        public CommentStatement() { Token = TokenType.Comment; }
        public string Text { get; set; } = string.Empty;
        public override string ToString()
        {
            return $"{Text}";
        }
    }
}