using System.Text.Json.Serialization;

namespace DaJet.Flow.Model
{
    public sealed class PipelineBlock
    {
        [JsonIgnore] public Guid Uuid { get; set; } = Guid.NewGuid();
        [JsonIgnore] public int Ordinal { get; set; } = 0;
        [JsonIgnore] public string Script { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Handler))] public string Handler { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Options))] public Dictionary<string, string> Options { get; set; } = new();
    }
}