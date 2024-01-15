using DaJet.Data;

namespace DaJet.Scripting
{
    public sealed class TranspilerResult
    {
        public string SqlScript { get; set; } = string.Empty;
        public List<EntityMapper> Mappers { get; set; } = new();
        public List<SqlStatement> Statements { get; set; } = new();
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
}