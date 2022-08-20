using System;
using System.Collections.Generic;

namespace DaJet.Metadata.Model
{
    public sealed class Enumeration : ApplicationObject
    {
        public List<EnumValue> Values { get; set; } = new List<EnumValue>();
    }
    public sealed class EnumValue
    {
        public Guid Uuid { get; set; } = Guid.Empty;
        public string Name { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;
    }
    
    //PropertyNameLookup.Add("_idrref", "Ссылка");
    //PropertyNameLookup.Add("_enumorder", "Порядок");
}