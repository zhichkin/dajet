namespace DaJet.Http.Model
{
    public sealed class ScriptModel
    {
        public Guid Uuid { get; set; }
        public string Name { get; set; }
        public bool IsFolder { get; set; }
        public Guid Parent { get; set; } // ScriptModel
        public string Owner { get; set; } // InfoBase
        public string Script { get; set; } = string.Empty;
        public List<ScriptModel> Children { get; set; } = new();
    }
}