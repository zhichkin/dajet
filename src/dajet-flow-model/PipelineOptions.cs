using System.Text.Json.Serialization;

namespace DaJet.Flow.Model
{
    public sealed class PipelineOptions
    {
        [JsonPropertyName(nameof(Uuid))] public Guid Uuid { get; set; } = Guid.NewGuid();
        [JsonPropertyName(nameof(Name))] public string Name { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Blocks))] public List<PipelineBlock> Blocks { get; set; } = new();
        [JsonPropertyName(nameof(Options))] public Dictionary<string, string> Options { get; set; } = new();
    }
}