using System.Text.Json.Serialization;

namespace DaJet.RabbitMQ.HttpApi
{
    public sealed class VirtualHostInfo
    {
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("description")] public string Description { get; set; }
    }
}

// [
//    {
//        "cluster_state": {
//            "rabbit@Zhichkin": "running"
//        },
//        "description": "Default virtual host",
//        "messages": 0,
//        "messages_details": {
//    "rate": 0.0
//        },
//        "messages_ready": 0,
//        "messages_ready_details": {
//    "rate": 0.0
//        },
//        "messages_unacknowledged": 0,
//        "messages_unacknowledged_details": {
//    "rate": 0.0
//        },
//        "metadata": {
//    "description": "Default virtual host",
//            "tags": []
//        },
//        "name": "/",
//        "tags": [],
//        "tracing": false
//    }
// ]