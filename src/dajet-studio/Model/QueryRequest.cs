namespace DaJet.Studio.Model
{
    public sealed class QueryRequest
    {
        public string DbName { get; set; } = string.Empty;
        public string Script { get; set; } = string.Empty;
    }
}