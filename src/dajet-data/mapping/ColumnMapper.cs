namespace DaJet.Data
{
    public sealed class ColumnMapper
    {
        public ColumnMapper() { }
        public ColumnMapper(string name) { Name = name; }
        public ColumnMapper(string name, string alias) : this(name) { Alias = alias; }
        public int Ordinal { get; set; } = -1; // ordinal position of column in IDataReader (may be undefined)
        public string Name { get; set; } = string.Empty; // name of column to get ordinal position in IDataReader
        public string Alias { get; set; } = string.Empty; // alias, if not empty, is used instead of the name
        public UnionTag Type { get; set; } = UnionTag.Undefined; // data type of column (purpose)
        public string TypeName { get; set; } = string.Empty; // database data type name
        public override string ToString() { return $"[{Ordinal}] {Name} {Type}"; }
    }
}