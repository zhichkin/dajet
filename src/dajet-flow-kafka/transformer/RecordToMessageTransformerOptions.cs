using DaJet.Model;

namespace DaJet.Flow.Kafka
{
    public sealed class RecordToMessageTransformerOptions : OptionsBase
    {
        public string PackageName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }
}