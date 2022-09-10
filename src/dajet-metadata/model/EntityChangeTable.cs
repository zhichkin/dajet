using System.Text.Json.Serialization;

namespace DaJet.Metadata.Model
{
    public sealed class ChangeTrackingTable : ApplicationObject
    {
        internal ChangeTrackingTable(ApplicationObject entity)
        {
            Entity = entity;
        }
        [JsonIgnore] public ApplicationObject Entity { get; }
    }
}