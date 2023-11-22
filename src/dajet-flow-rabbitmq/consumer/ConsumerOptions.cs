using DaJet.Model;
using System.ComponentModel.DataAnnotations;

namespace DaJet.Flow.RabbitMQ
{
    public sealed class ConsumerOptions : OptionsBase
    {
        [Required] public string Source { get; set; } = "amqp://guest:guest@localhost:5672/%2F";
        [Required] public string Queue { get; set; } = string.Empty;
        public int Heartbeat { get; set; } = 60; // seconds (consumer health check periodicity)
    }
}