using DaJet.Model;
using System.ComponentModel.DataAnnotations;

namespace DaJet.Flow.RabbitMQ
{
    public sealed class ProducerOptions : OptionsBase
    {
        
        [Required] public string Target { get; set; } = "amqp://guest:guest@localhost:5672/%2F";
        [Required] public string Exchange { get; set; } = string.Empty; // if exchange name is empty, then RoutingKey is a queue name to send directly
        public string RoutingKey { get; set; } = string.Empty; // if exchange name is not empty, then this is routing key value, otherwise direct queue
        public string Sender { get; set; } = string.Empty; // Sender application identifier (AppId)
        public string MessageType { get; set; } = string.Empty; // Message type identifier (Type)
        public string CC { get; set; } = string.Empty; // additional routing keys not seen by consumers (csv)
        public string BCC { get; set; } = string.Empty; // additional routing keys seen by consumers (csv)
        public bool Mandatory { get; set; } = false; // helps to detect unroutable messages, firing BasicReturn event on producer
    }
}