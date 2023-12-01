using DaJet.Data;

namespace DaJet.Scripting
{
    public sealed class ColumnMappingRule
    {
        public ColumnMapper Target { get; set; }
        public object Source { get; set; }
        public override string ToString()
        {
            return $"{Target} <- {(Source is null ? "NULL" : Source.ToString())}";
        }
    }
}