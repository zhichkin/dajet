using DaJet.Data;

namespace DaJet.Scripting
{
    public sealed class PropertyMappingRule
    {
        public PropertyMapper Target { get; set; }
        public PropertyMapper Source { get; set; }
        public List<ColumnMappingRule> Columns { get; set; }
        public override string ToString()
        {
            return $"{Target} <- {(Source is null ? "NULL" : Source.ToString())}";
        }
    }
}