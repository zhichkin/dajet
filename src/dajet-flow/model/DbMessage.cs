namespace DaJet.Flow
{
    public sealed class DbMessage
    {
        public Guid Uuid { get; set; }
        public decimal Number { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty;
    }
}