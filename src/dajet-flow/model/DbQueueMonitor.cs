namespace DaJet.Flow
{
    public sealed class DbQueueMonitor
    {
        public DateTime TimeStamp { get; set; }
        public long MessageCount { get; set; }
        public long DataSizeSum { get; set; }
        public long DataSizeAvg { get; set; }
        public string MessageType { get; set; } = string.Empty;
    }
}