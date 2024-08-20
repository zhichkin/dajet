namespace DaJet.Scripting.Model
{
    public sealed class CaseStatement : SyntaxNode
    {
        public CaseStatement() { Token = TokenType.CASE; }
        public List<WhenClause> CASE { get; set; } = new();
        public StatementBlock ELSE { get; set; } = new();
    }
}