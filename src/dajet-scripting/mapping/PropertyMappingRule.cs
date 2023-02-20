namespace DaJet.Scripting
{
    public sealed class PropertyMappingRule
    {
        public PropertyMap Target { get; set; }
        public PropertyMap Source { get; set; }
        public List<ColumnMappingRule> Columns { get; set; }
        public override string ToString()
        {
            return $"{Target} <- {(Source is null ? "NULL" : Source.ToString())}";
        }
    }
}