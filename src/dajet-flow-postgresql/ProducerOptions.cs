using DaJet.Model;

namespace DaJet.Flow.PostgreSql
{
    public sealed class ProducerOptions : OptionsBase
    {
        public string Target { get; set; } = string.Empty;
        public string Script { get; set; } = string.Empty;
        public int Timeout { get; set; } = 10; // seconds (value of 0 indicates no limit)
    }
}