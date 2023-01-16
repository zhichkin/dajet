namespace DaJet.Studio.Model
{
    public sealed class EnumModel
    {
        public int TypeCode { get; set; } = 0;
        public Guid Uuid { get; set; } = Guid.Empty;
        public string Name { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public List<EnumValue> Values { get; set; } = new();
        public List<PropertyModel> Properties { get; set; } = new();
    }
    public sealed class EnumValue
    {
        public Guid Uuid { get; set; } = Guid.Empty;
        public string Name { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;
    }
}