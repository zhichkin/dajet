using System.Text.Json.Serialization;

namespace DaJet.Model.Http
{
    public sealed class CreateDbViewsRequest
    {
        [JsonPropertyName(nameof(Schema))] public string Schema { get; set; } = string.Empty;
    }
}