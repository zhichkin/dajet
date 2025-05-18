namespace DaJet.Http.Model
{
    public sealed class EntityModel
    {
        public int TypeCode { get; set; } = 0;
        public Guid Uuid { get; set; } = Guid.Empty;
        public string Name { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public List<PropertyModel> Properties { get; set; } = new();
        public List<EntityModel> TableParts { get; set; } = new();
    }
}