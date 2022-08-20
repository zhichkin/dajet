namespace DaJet.Scripting.Model
{
    public abstract class SyntaxNode
    {
        public TokenType Token { get; set; } = TokenType.Ignore;
        public override string ToString()
        {
            return $"{Token}: {GetType()}";
        }
    }
}