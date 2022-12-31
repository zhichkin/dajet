namespace DaJet.Studio.Model
{
    public sealed class PropertyModel
    {
        public string Name { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string DbName { get; set; } = string.Empty;
        public List<ColumnModel> Columns { get; set; } = new();
        public DataTypeModel PropertyType { get; set; } = new();
        public DataTypeModel ExtensionPropertyType { get; set; } = new();
        public PropertyPurpose Purpose { get; set; } = PropertyPurpose.Property;
    }
}