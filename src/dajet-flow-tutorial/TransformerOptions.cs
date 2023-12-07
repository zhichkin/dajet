using DaJet.Model;
using System.ComponentModel.DataAnnotations;

namespace DaJet.Flow.Tutorial
{
    public sealed class TransformerOptions : OptionsBase
    {
        [Required] public string ContentType { get; set; } = "application/json";
    }
}