using Confluent.Kafka;
using DaJet.Model;

namespace DaJet.Flow.Kafka
{
    public sealed class ProducerOptions : OptionsBase
    {
        public string Topic { get; set; } = string.Empty;
        public ProducerConfig Config { get; set; }
    }
}