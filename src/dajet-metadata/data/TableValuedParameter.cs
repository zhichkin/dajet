using System.Collections.Generic;

namespace DaJet.Data
{
    public sealed class TableValuedParameter
    {
        public string Name { get; set; }
        public string DbName { get; set; }
        public List<Dictionary<string, object>> Value { get; set; }
    }
}