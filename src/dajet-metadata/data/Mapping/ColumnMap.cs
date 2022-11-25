namespace DaJet.Data.Mapping
{
    public sealed class ColumnMap
    {
        #region "CONSTRUCTORS"
        public ColumnMap() { }
        public ColumnMap(string name)
        {
            Name = name;
        }
        public ColumnMap(string name, string alias) : this(name)
        {
            Alias = alias;
        }
        public ColumnMap(string name, ColumnPurpose purpose) : this(name)
        {
            Purpose = purpose;
        }
        public ColumnMap(string name, string alias, ColumnPurpose purpose) : this(name, alias)
        {
            Purpose = purpose;
        }
        #endregion
        public int Ordinal { get; set; } = -1; // ordinal position of column in IDataReader (reserved for the future)
        public string Name { get; set; } = string.Empty; // name of column to get ordinal position in IDataReader
        public string Alias { get; set; } = string.Empty; // alias, if not empty, is used instead of the name
        public ColumnPurpose Purpose { get; set; } = ColumnPurpose.Default; // ordinary column to store single value of the defined DbType
    }
}