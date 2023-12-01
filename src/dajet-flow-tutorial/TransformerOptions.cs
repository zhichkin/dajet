using DaJet.Model;
using System.ComponentModel.DataAnnotations;

namespace DaJet.Flow.Tutorial
{
    public sealed class TransformerOptions : OptionsBase
    {
        [Required] public string MessageType { get; set; } = string.Empty;
    }
}