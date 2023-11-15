using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DaJet.Model
{
    public sealed class HandlerModel
    {
        [JsonIgnore] public Guid Uuid { get; set; } = Guid.NewGuid();
        [JsonIgnore] public int Ordinal { get; set; } = 0;
        [JsonPropertyName(nameof(Name))] public string Name { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Input))] public string Input { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Output))] public string Output { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Options))] public List<OptionModel> Options { get; set; } = new();
    }
}