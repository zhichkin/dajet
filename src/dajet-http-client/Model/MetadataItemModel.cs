namespace DaJet.Http.Model
{
    public sealed class MetadataItemModel
    {
        public Guid Type { get; set; } = Guid.Empty;
        public Guid Uuid { get; set; } = Guid.Empty;
        public string Name { get; set; } = string.Empty;
    }
}