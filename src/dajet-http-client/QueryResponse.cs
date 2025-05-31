using System.Text.Json.Serialization;

namespace DaJet.Http.Client
{
    public sealed class QueryResponse
    {
        [JsonPropertyName(nameof(Success))] public bool Success { get; set; } = false;
        [JsonPropertyName(nameof(Error))] public string Error { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Message))] public string Message { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Script))] public string Script { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Result))] public object Result { get; set; } = string.Empty;
    }
}