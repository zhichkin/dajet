using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DaJet.Model
{
    public sealed class HandlerModel
    {
        [JsonIgnore] public Guid Uuid { get; set; } = Guid.NewGuid();
        [JsonIgnore] public int Ordinal { get; set; } = 0;
        [JsonPropertyName(nameof(Handler))] public string Handler { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Message))] public string Message { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Options))] public List<OptionModel> Options { get; set; } = new();
    }
}