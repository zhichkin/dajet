using System.Text.Json.Serialization;

namespace DaJet.Flow
{
    public sealed class PipelineOptions
    {
        [JsonPropertyName(nameof(Uuid))] public Guid Uuid { get; set; } = Guid.NewGuid();
        [JsonPropertyName(nameof(Name))] public string Name { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Blocks))] public List<PipelineBlock> Blocks { get; set; } = new();
        [JsonPropertyName(nameof(Options))] public Dictionary<string, string> Options { get; set; } = new();
    }
    public sealed class PipelineBlock
    {
        [JsonIgnore] public Guid Uuid { get; set; } = Guid.NewGuid();
        [JsonIgnore] public int Ordinal { get; set; } = 0;
        [JsonIgnore] public string Script { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Handler))] public string Handler { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Options))] public Dictionary<string, string> Options { get; set; } = new();
    }
}