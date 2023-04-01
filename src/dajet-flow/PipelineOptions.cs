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
        [JsonPropertyName(nameof(Uuid))] public Guid Uuid { get; set; } = Guid.NewGuid();
        [JsonPropertyName(nameof(Ordinal))] public int Ordinal { get; set; } = 0;
        [JsonPropertyName(nameof(Script))] public string Script { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Handler))] public string Handler { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Options))] public Dictionary<string, string> Options { get; set; } = new();
        [JsonIgnore] internal Type HandlerType { get; set; }
        [JsonIgnore] internal object HandlerInstance { get; set; }
    }
}