using System.Text.Json.Serialization;

namespace DaJet.RabbitMQ.HttpApi
{
    internal sealed class BindingRequest
    {
        [JsonPropertyName("routing_key")] public string RoutingKey { get; set; }
    }
}