using System.Text.Json.Serialization;

namespace DaJet.Http.Client
{
    public sealed class QueryRequest
    {
        [JsonPropertyName(nameof(DbName))] public string DbName { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Script))] public string Script { get; set; } = string.Empty;
    }
}