using System.Collections.Generic;

namespace DaJet.Model
{
    public sealed class ProcessorInfo
    {
        public string Handler { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<OptionInfo> Options { get; set; } = new();
    }
}