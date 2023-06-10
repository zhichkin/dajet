namespace DaJet.Scripting.Model
{
    public sealed class TableReference : SyntaxNode
    {
        public TableReference() { Token = TokenType.Table; }
        public string Alias { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public object Binding { get; set; }
        public override string ToString()
        {
            return $"[{Token}: {Identifier}]";
        }
        public string Hints { get; set; } // ms like WITH table hints
    }
}