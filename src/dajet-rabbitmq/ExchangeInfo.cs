using System.Text.Json.Serialization;

namespace DaJet.RabbitMQ.HttpApi
{
    public sealed class ExchangeInfo
    {
        [JsonPropertyName("auto_delete")] public bool AutoDelete { get; set; }
        [JsonPropertyName("durable")] public bool Durable { get; set; }
        [JsonPropertyName("internal")] public bool Internal { get; set; }
        [JsonPropertyName("vhost")] public string VirtualHost { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("type")] public string Type { get; set; }
    }
}

// {
//  "arguments": { },
//  "auto_delete": false,
//  "durable": true,
//  "internal": false,
//  "message_stats": {
//    "publish_in": 101647342,
//    "publish_in_details": { "rate": 0.0 },
//    "publish_out": 101647342,
//    "publish_out_details": { "rate": 0.0 }
//  },
//  "name": "РИБ.ЦБ.ЦА",
//  "type": "direct",
//  "user_who_performed_action": "admin",
//  "vhost": "/"
// }