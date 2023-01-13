namespace DaJet.Studio.Model
{
    public sealed class ScriptModel
    {
        public Guid Uuid { get; set; } = Guid.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsFolder { get; set; } = true;
        public Guid Parent { get; set; } = Guid.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Script { get; set; } = string.Empty;
        public List<ScriptModel> Children { get; set; } = new();
    }
}