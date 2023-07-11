using System.Text.Json.Serialization;

namespace DaJet.RabbitMQ.HttpApi
{
    public sealed class QueueInfo
    {
        [JsonPropertyName("node")] public string Node { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("type")] public string Type { get; set; }
        [JsonPropertyName("durable")] public bool Durable { get; set; }
        [JsonPropertyName("vhost")] public string VirtualHost { get; set; }
    }
}

//  {
//    "arguments": { },
//    "auto_delete": false,
//    "backing_queue_status": {
//        "avg_ack_egress_rate": 0.0,
//      "avg_ack_ingress_rate": 0.0,
//      "avg_egress_rate": 0.0,
//      "avg_ingress_rate": 0.0,
//      "delta": [ "delta", 0, 0, 0, 0 ],
//      "len": 0,
//      "mode": "default",
//      "next_seq_id": 1,
//      "q1": 0,
//      "q2": 0,
//      "q3": 0,
//      "q4": 0,
//      "target_ram_count": "infinity"
//    },
//    "consumer_utilisation": null,
//    "consumers": 0,
//    "durable": true,
//    "effective_policy_definition": { },
//    "exclusive": false,
//    "exclusive_consumer_tag": null,
//    "garbage_collection": {
//        "fullsweep_after": 65535,
//      "max_heap_size": 0,
//      "min_bin_vheap_size": 46422,
//      "min_heap_size": 233,
//      "minor_gcs": 375
//    },
//    "head_message_timestamp": null,
//    "idle_since": "2021-08-07 17:38:19",
//    "memory": 18500,
//    "message_bytes": 0,
//    "message_bytes_paged_out": 0,
//    "message_bytes_persistent": 0,
//    "message_bytes_ram": 0,
//    "message_bytes_ready": 0,
//    "message_bytes_unacknowledged": 0,
//    "message_stats": {
//        "ack": 1,
//      "ack_details": { "rate": 0.0 },
//      "deliver": 1,
//      "deliver_details": { "rate": 0.0 },
//      "deliver_get": 2,
//      "deliver_get_details": { "rate": 0.0 },
//      "deliver_no_ack": 0,
//      "deliver_no_ack_details": { "rate": 0.0 },
//      "get": 1,
//      "get_details": { "rate": 0.0 },
//      "get_empty": 0,
//      "get_empty_details": { "rate": 0.0 },
//      "get_no_ack": 0,
//      "get_no_ack_details": { "rate": 0.0 },
//      "publish": 1,
//      "publish_details": { "rate": 0.0 },
//      "redeliver": 1,
//      "redeliver_details": { "rate": 0.0 }
//    },
//    "messages": 0,
//    "messages_details": { "rate": 0.0 },
//    "messages_paged_out": 0,
//    "messages_persistent": 0,
//    "messages_ram": 0,
//    "messages_ready": 0,
//    "messages_ready_details": { "rate": 0.0 },
//    "messages_ready_ram": 0,
//    "messages_unacknowledged": 0,
//    "messages_unacknowledged_details": { "rate": 0.0 },
//    "messages_unacknowledged_ram": 0,
//    "name": "РИБ.N002.MAIN",
//    "node": "rabbit@Zhichkin",
//    "operator_policy": null,
//    "policy": null,
//    "recoverable_slaves": null,
//    "reductions": 2165007,
//    "reductions_details": { "rate": 0.0 },
//    "single_active_consumer_tag": null,
//    "state": "running",
//    "type": "classic",
//    "vhost": "/"
//  }