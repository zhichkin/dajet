using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DaJet.Model.Http
{
    public sealed class CreateDbViewsResponse
    {
        [JsonPropertyName(nameof(Result))] public int Result { get; set; }
        [JsonPropertyName(nameof(Errors))] public List<string> Errors { get; set; } = new();
    }
}