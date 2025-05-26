using DaJet.Model;
using System.ComponentModel.DataAnnotations;

namespace DaJet.Flow.Script
{
    public sealed class ScriptOptions : OptionsBase
    {
        [Required] public string Script { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; }
    }
}