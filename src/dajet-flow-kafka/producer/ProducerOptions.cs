using Confluent.Kafka;
using DaJet.Model;
using System.ComponentModel.DataAnnotations;

namespace DaJet.Flow.Kafka
{
    public sealed class ProducerOptions : OptionsBase
    {
        [Required] public string Topic { get; set; } = string.Empty;
        public ProducerConfig Config { get; set; }
    }
}