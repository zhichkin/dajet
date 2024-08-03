using System.Text.Json.Serialization;

namespace DaJet.Metadata.Model
{
    public sealed class RegisterTotalsTable : ApplicationObject
    {
        internal RegisterTotalsTable(in ApplicationObject entity)
        {
            Entity = entity;
        }
        [JsonIgnore] public ApplicationObject Entity { get; }
    }
}