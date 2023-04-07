using System.Text.Json.Serialization;

namespace DaJet.Flow.Model
{
    public sealed class PipelineBlock
    {
        [JsonIgnore] public Guid Uuid { get; set; } = Guid.NewGuid();
        [JsonIgnore] public int Ordinal { get; set; } = 0;
        [JsonPropertyName(nameof(Handler))] public string Handler { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Message))] public string Message { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Options))] public List<OptionItem> Options { get; set; } = new();
    }
}