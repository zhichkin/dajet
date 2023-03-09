namespace DaJet.Scripting.Model
{
    public sealed class ColumnDefinition : SyntaxNode
    {
        public ColumnDefinition() { Token = TokenType.COLUMN; }
        public string Name { get; set; }
        public TypeIdentifier Type { get; set; }
        public bool IsIdentity { get; set; } = false;
        public bool IsNullable { get; set; } = false;
    }
}