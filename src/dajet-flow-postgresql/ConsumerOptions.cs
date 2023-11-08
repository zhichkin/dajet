using DaJet.Model;

namespace DaJet.Flow.PostgreSql
{
    public sealed class ConsumerOptions : HandlerOptions
    {
        public string Source { get; set; } = string.Empty;
        public string Script { get; set; } = string.Empty;
        public int Timeout { get; set; } = 10; // seconds (value of 0 indicates no limit)
    }
}