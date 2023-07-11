using System.Text.Json.Serialization;

namespace DaJet.RabbitMQ.HttpApi
{
    public sealed class BindingInfo
    {
        [JsonPropertyName("source")] public string Source { get; set; }
        [JsonPropertyName("vhost")] public string VirtualHost { get; set; }
        [JsonPropertyName("destination")] public string Destination { get; set; }
        [JsonPropertyName("destination_type")] public string DestinationType { get; set; } // queue | exchange
        [JsonPropertyName("routing_key")] public string RoutingKey { get; set; }
        [JsonPropertyName("properties_key")] public string PropertiesKey { get; set; }
    }
}

// {
//  "source": "РИБ.0120.ЦБ",
//  "vhost": "/",
//  "destination": "РИБ.0120.ЦБ",
//  "destination_type": "queue",
//  "routing_key": "",
//  "arguments": { },
//  "properties_key": "~"
// }