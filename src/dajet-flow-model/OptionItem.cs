using System.Text.Json.Serialization;

namespace DaJet.Flow.Model
{
    public sealed class OptionItem
    {
        [JsonPropertyName(nameof(Name))] public string Name { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Type))] public string Type { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Value))] public string Value { get; set; } = string.Empty;
    }
}