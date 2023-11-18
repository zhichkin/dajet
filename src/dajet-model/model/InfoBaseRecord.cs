using DaJet.Data;
using System.Text.Json.Serialization;

namespace DaJet.Model
{
    public sealed class InfoBaseRecord : EntityObject
    {
        [JsonPropertyName(nameof(Name))] public string Name { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Description))] public string Description { get; set; } = string.Empty;
        [JsonPropertyName(nameof(UseExtensions))] public bool UseExtensions { get; set; } = true;
        [JsonPropertyName(nameof(DatabaseProvider))] public string DatabaseProvider { get; set; } = string.Empty;
        [JsonPropertyName(nameof(ConnectionString))] public string ConnectionString { get; set; } = string.Empty;
        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? base.ToString() : Name;
        }
    }
}