using System.Collections.Generic;

namespace DaJet.Data
{
    public sealed class QueryObject
    {
        public string Query { get; set; }
        public string Script { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }
}