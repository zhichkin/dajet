using DaJet.Data;

namespace DaJet.Metadata.Model
{
    public sealed class MetadataColumn
    {
        public MetadataColumn() { }
        public MetadataColumn(string name, string typeName, int length)
        {
            Name = name;
            Length = length;
            TypeName = typeName;
        }
        public MetadataColumn(string name, string typeName, int length, int precision, int scale) : this(name, typeName, length)
        {
            Scale = scale;
            Precision = precision;
        }
        public string Name { get; set; }
        public ColumnPurpose Purpose { get; set; } = ColumnPurpose.Default;
        public string TypeName { get; set; }
        public int Length { get; set; }
        public int Precision { get; set; }
        public int Scale { get; set; }
        public bool IsNullable { get; set; }
        public int KeyOrdinal { get; set; }
        public bool IsPrimaryKey { get; set; }
        public override string ToString() { return Name; }
    }
}