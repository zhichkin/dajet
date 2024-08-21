namespace DaJet.Scripting.Model
{
    public sealed class ExecuteStatement : SyntaxNode
    {
        public ExecuteStatement() { Token = TokenType.EXECUTE; }
        public string Uri { get; set; } // script uri template
        public override string ToString()
        {
            return $"EXECUTE {Uri}";
        }
    }
}