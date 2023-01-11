namespace DaJet.Studio.Model
{
    public sealed class ScriptModel
    {
        public string Uuid { get; set; }
        public string Name { get; set; }
        public bool IsFolder { get; set; }
        public List<ScriptModel> Children { get; set; } = new();
    }
}