namespace DaJet.Scripting.Model
{
    public sealed class CommentStatement : SyntaxNode
    {
        public CommentStatement()
        {
            Token = TokenType.Comment;
        }
        public string Text { get; set; }
        public override string ToString()
        {
            return $"{Text}";
        }
    }
}