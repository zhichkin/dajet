using System.Text.Json.Serialization;

namespace DaJet.Flow.Model
{
    public sealed class PipelineInfo
    {
        [JsonPropertyName(nameof(Uuid))] public Guid Uuid { get; set; } = Guid.Empty;
        [JsonPropertyName(nameof(Name))] public string Name { get; set; } = string.Empty;
    }
}