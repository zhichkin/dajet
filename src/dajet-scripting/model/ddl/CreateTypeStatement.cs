namespace DaJet.Scripting.Model
{
    public sealed class CreateTypeStatement : SyntaxNode
    {
        public CreateTypeStatement() { Token = TokenType.TYPE; }
        public string Name { get; set; } = string.Empty;
        public string BaseType { get; set; } = string.Empty;
        public string NestType { get; set; } = string.Empty;
        public List<string> PrimaryKey { get; set; } = new();
        public List<string> DropColumns { get; set; }
        public List<ColumnDefinition> AlterColumns { get; set; }
        public List<ColumnDefinition> Columns { get; } = new();
    }
}