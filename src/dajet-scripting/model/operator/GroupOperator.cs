namespace DaJet.Scripting.Model
{
    public sealed class GroupOperator : SyntaxNode
    {
        public GroupOperator() { Token = TokenType.OpenRoundBracket; }
        public SyntaxNode Expression { get; set; }
    }
}