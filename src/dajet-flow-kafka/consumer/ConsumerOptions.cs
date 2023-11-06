using Confluent.Kafka;
using DaJet.Model;

namespace DaJet.Flow.Kafka
{
    public sealed class ConsumerOptions : OptionsBase
    {
        public Guid Pipeline { get; set; } = Guid.Empty;
        public string Topic { get; set; } = string.Empty;
        public ConsumerConfig Config { get; set; }
    }
}