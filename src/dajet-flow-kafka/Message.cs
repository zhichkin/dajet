using Confluent.Kafka;

namespace DaJet.Flow.Kafka
{
    public sealed class Message
    {
        public Payload Payload { get; set; }
        public Headers Headers { get; set; } = new Headers();
    }
}