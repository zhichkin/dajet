using System.Text.Json.Serialization;

namespace DaJet.Metadata.Model
{
    public sealed class EntityChangeTable : ApplicationObject
    {
        internal EntityChangeTable(ApplicationObject entity)
        {
            Entity = entity;
        }
        [JsonIgnore] public ApplicationObject Entity { get; }
    }
}