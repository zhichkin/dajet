namespace DaJet.Scripting.Model
{
    public sealed class TableReference : SyntaxNode
    {
        public TableReference() { Token = TokenType.Table; }
        public object Tag { get; set; }
        public string Alias { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public override string ToString()
        {
            return $"[{Token}: {Identifier}]";
        }
    }
}