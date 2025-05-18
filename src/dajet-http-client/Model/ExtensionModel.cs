namespace DaJet.Http.Model
{
    public sealed class ExtensionModel
    {
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string Version { get; set; } = string.Empty;
        public DateTime Updated { get; set; } = DateTime.MinValue;
    }
}