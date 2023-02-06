using System.Text.Json.Serialization;

namespace DaJet.Scripting
{
    public sealed class GeneratorResult
    {
        public bool Success { get; set; } = true;
        public string Error { get; set; } = string.Empty;
        public string Script { get; set; } = string.Empty;
        [JsonIgnore] public EntityMap Mapper { get; set; } = new();
    }
}