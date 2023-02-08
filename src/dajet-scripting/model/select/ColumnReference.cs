namespace DaJet.Scripting.Model
{
    public sealed class ColumnReference : SyntaxNode
    {
        public ColumnReference() { Token = TokenType.Column; }
        public string Identifier { get; set; } = string.Empty;
        public object Binding { get; set; }
        public List<ColumnMap> Mapping { get; set; }
        public override string ToString()
        {
            return $"[{Token}: {Identifier}]";
        }
    }
}