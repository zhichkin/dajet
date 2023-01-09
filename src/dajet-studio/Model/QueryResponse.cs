namespace DaJet.Studio.Model
{
    public sealed class QueryResponse
    {
        public bool Success { get; set; } = false;
        public string Error { get; set; } = string.Empty;
        public string Script { get; set; } = string.Empty;
        public object Result { get; set; } = string.Empty;
    }
}