using System;
using System.Text.Json.Serialization;

namespace DaJet.Model
{
    public sealed class PipelineInfo
    {
        [JsonPropertyName(nameof(Uuid))] public Guid Uuid { get; set; } = Guid.Empty;
        [JsonPropertyName(nameof(Name))] public string Name { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Status))] public string Status { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Start))] public DateTime Start { get; set; } = DateTime.MinValue;
        [JsonPropertyName(nameof(Finish))] public DateTime Finish { get; set; } = DateTime.MinValue;
        [JsonPropertyName(nameof(State))] public PipelineState State { get; set; } = PipelineState.Idle;
        [JsonPropertyName(nameof(Activation))] public ActivationMode Activation { get; set; } = ActivationMode.Manual;
    }
}