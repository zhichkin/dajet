using DaJet.Model;

namespace DaJet.Flow.Kafka
{
    public sealed class MessageToRecordTransformerOptions : OptionsBase
    {
        public string PackageName { get; set; } = string.Empty;
        public string MessageType { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }
}