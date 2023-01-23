using System.Text.Json.Serialization;
using System;

namespace DaJet.Http.Model
{
    public sealed class QueryModel
    {
        [JsonPropertyName(nameof(DbName))] public string DbName { get; set; } = string.Empty;
        [JsonPropertyName(nameof(Script))] public string Script { get; set; } = string.Empty;
    }
}