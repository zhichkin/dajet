namespace DaJet.Data
{
    public sealed class TypeColumnInfo
    {
        public string Name { get; set; } = string.Empty;
        public int Ordinal { get; set; }
        public string Type { get; set; } = string.Empty;
        public int MaxLength { get; set; }
        public int Precision { get; set; }
        public int Scale { get; set; }
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
    }
}