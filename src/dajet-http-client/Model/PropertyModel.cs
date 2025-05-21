namespace DaJet.Http.Model
{
    public sealed class PropertyModel
    {
        public Guid Uuid { get; set; } = Guid.Empty;
        public string Name { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public List<TypeModel> DataType { get; set; } = new();
        public List<ColumnModel> Columns { get; set; } = new();
        public List<ReferenceModel> References { get; set; } = new();
    }
}