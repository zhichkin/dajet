using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DaJet.Model
{
    public sealed class PipelineModel
    {
        [JsonIgnore] public Guid Uuid { get; set; } = Guid.NewGuid();
        [JsonPropertyName(nameof(Name))] public string Name { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Activation))] public ActivationMode Activation { get; set; } = ActivationMode.Manual;
        [JsonPropertyName(nameof(Options))] public List<OptionModel> Options { get; set; } = new();
        [JsonPropertyName(nameof(Handlers))] public List<HandlerModel> Handlers { get; set; } = new();
    }
}