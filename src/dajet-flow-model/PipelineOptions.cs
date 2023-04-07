using System.Text.Json.Serialization;

namespace DaJet.Flow.Model
{
    public sealed class PipelineOptions
    {
        [JsonPropertyName(nameof(Uuid))] public Guid Uuid { get; set; } = Guid.NewGuid();
        [JsonPropertyName(nameof(Name))] public string Name { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Activation))] public ActivationMode Activation { get; set; } = ActivationMode.Manual;
        [JsonPropertyName(nameof(Options))] public List<OptionItem> Options { get; set; } = new();
        [JsonPropertyName(nameof(Blocks))] public List<PipelineBlock> Blocks { get; set; } = new();
    }
}