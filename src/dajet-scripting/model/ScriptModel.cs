namespace DaJet.Scripting.Model
{
    public sealed class ScriptModel : SyntaxNode
    {
        public ScriptModel()
        {
            Token = TokenType.Script;
        }
        public List<SyntaxNode> Statements { get; } = new();
    }
}