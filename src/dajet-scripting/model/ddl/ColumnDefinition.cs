namespace DaJet.Scripting.Model
{
    public sealed class ColumnDefinition : SyntaxNode
    {
        public ColumnDefinition() { Token = TokenType.COLUMN; }
        public string Name { get; set; }
        public TypeIdentifier Type { get; set; }
        public bool IsIdentity { get; set; } = false;
        public int IdentitySeed { get; set; } = 1;
        public int IdentityIncrement { get; set; } = 1;
        public bool IsNullable { get; set; } = false;
        public bool IsVersion { get; set; } = false;
    }
}