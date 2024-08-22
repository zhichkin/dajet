using System.Text.Json.Serialization;

namespace DaJet.Model
{
    public sealed class CodeItem
    {
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("folder")] public bool IsFolder { get; set; }
    }
}