namespace DaJet.Scripting.Model
{
    public sealed class ColumnDefinition : SyntaxNode
    {
        public ColumnDefinition() { Token = TokenType.COLUMN; }
        public string Name { get; set; }
        public TypeIdentifier Type { get; set; }
    }
}