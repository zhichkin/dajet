using System.Text.Json.Serialization;

namespace DaJet.Model
{
    public sealed class OptionModel
    {
        [JsonPropertyName(nameof(Name))] public string Name { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Type))] public string Type { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Value))] public string Value { get; set; } = string.Empty;
        [JsonPropertyName(nameof(IsRequired))] public bool IsRequired { get; set; } = false;
    }
}