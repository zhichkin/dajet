using Confluent.Kafka;
using DaJet.Model;

namespace DaJet.Flow.Kafka
{
    public sealed class ConsumerOptions : OptionsBase
    {
        public string Topic { get; set; } = string.Empty;
        public ConsumerConfig Config { get; set; }
    }
}