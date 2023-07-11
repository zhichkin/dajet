using System.Data;

namespace DaJet.Exchange
{
    public sealed class OneDbMessage
    {
        public Guid Uuid { get; set; } = Guid.NewGuid();
        public long Sequence { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public IDataRecord DataRecord { get; set; }
        public string Sender { get; set; } = string.Empty;
        public List<string> Subscribers { get; } = new();
        public string Payload { get; set; } = string.Empty;
    }
}