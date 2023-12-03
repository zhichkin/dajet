using DaJet.Model;
using System.ComponentModel.DataAnnotations;

namespace DaJet.Flow.Tutorial
{
    public sealed class StarterOptions : OptionsBase
    {
        [Required] public string Source { get; set; } = string.Empty;
        public string Metadata { get; set; } = string.Empty;
    }
}