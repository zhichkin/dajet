namespace DaJet.Scripting.Model
{
    public sealed class ColumnReference : SyntaxNode
    {
        public ColumnReference() { Token = TokenType.Column; }
        public string Identifier { get; set; } = string.Empty;
        public object Binding { get; set; }
        public List<ColumnMap> Mapping { get; set; }
        public string GetName()
        {
            string[] names = Identifier.Split('.', StringSplitOptions.RemoveEmptyEntries);

            if (names.Length == 0) { return string.Empty; }

            return names[names.Length - 1];
        }
        public override string ToString()
        {
            return $"[{Token}: {Identifier}]";
        }
    }
}