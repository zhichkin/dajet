using System.Data;

namespace DaJet.Stream
{
    public sealed class OneDbMessage
    {
        public int Sequence { get; set; }
        public int TypeCode { get; set; }
        public IDataRecord DataRecord { get; set; }
        public string Sender { get; set; }
        public List<string> Subscribers { get; } = new();
        public string Payload { get; set; }
    }
}