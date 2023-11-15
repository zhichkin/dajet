using DaJet.Model;
using System.ComponentModel.DataAnnotations;

namespace DaJet.Flow.SqlServer
{
    public sealed class ProducerOptions : OptionsBase
    {
        [Required] public string Target { get; set; } = string.Empty;
        [Required] public string Script { get; set; } = string.Empty;
        public int Timeout { get; set; } = 10; // seconds (value of 0 indicates no limit)
    }
}