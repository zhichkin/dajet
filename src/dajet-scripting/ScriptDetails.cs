using DaJet.Data;

namespace DaJet.Scripting
{
    public sealed class ScriptDetails
    {
        public string SqlScript { get; set; }
        public List<EntityMapper> Mappers { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }
}