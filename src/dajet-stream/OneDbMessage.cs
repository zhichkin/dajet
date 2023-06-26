using System.Data;

namespace DaJet.Stream
{
    public sealed class OneDbMessage
    {
        public Guid Session { get; set; }
        public int Sequence { get; set; }
        public int TypeCode { get; set; }
        public IDataRecord DataRecord { get; set; }
        public List<string> Subscribers { get; } = new();
        public string ContentType { get; set; } = string.Empty;
    }
}