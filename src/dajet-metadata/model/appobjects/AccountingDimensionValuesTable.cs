using System.Text.Json.Serialization;

namespace DaJet.Metadata.Model
{
    public sealed class AccountingDimensionValuesTable : ApplicationObject
    {
        internal AccountingDimensionValuesTable(in ApplicationObject entity)
        {
            Entity = entity;
        }
        [JsonIgnore] public ApplicationObject Entity { get; }
    }
}