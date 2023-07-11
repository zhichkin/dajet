using System.Text.Json.Serialization;

namespace DaJet.RabbitMQ.HttpApi
{
    internal sealed class QueueRequest
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "classic";
        [JsonPropertyName("durable")] public bool Durable { get; set; } = true;
        [JsonPropertyName("exclusive")] public bool Exclusive { get; set; } = false;
        [JsonPropertyName("auto_delete")] public bool AutoDelete { get; set; } = false;
    }
}