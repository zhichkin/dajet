using DaJet.Model;
using System.ComponentModel.DataAnnotations;

namespace DaJet.Flow.Tutorial
{
    public sealed class ProducerOptions : OptionsBase
    {
        [Required] public string Target { get; set; } = string.Empty;
        [Required] public string QueueName { get; set; } = string.Empty;
    }
}