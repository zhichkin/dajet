using System.Text.Json.Serialization;

namespace DaJet.Metadata.Model
{
    public sealed class RegisterSettingsTable : ApplicationObject
    {
        internal RegisterSettingsTable(in ApplicationObject entity)
        {
            Entity = entity;
        }
        [JsonIgnore] public ApplicationObject Entity { get; }
    }
}