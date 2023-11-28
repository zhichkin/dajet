using DaJet.Model;
using System.ComponentModel.DataAnnotations;

namespace DaJet.Flow.Tutorial
{
    public sealed class StarterOptions : OptionsBase
    {
        [Required] public string Name { get; set; } = string.Empty;
        public string Greeting { get; set; } = "Привет";
    }
}