namespace DaJet.Data.Client
{
    internal sealed class ScriptDetails
    {
        internal string SqlScript { get; set; }
        internal List<EntityMapper> Mappers { get; set; }
        internal Dictionary<string, object> Parameters { get; set; }
    }
}