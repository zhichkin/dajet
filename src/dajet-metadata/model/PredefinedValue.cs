using System;
using System.Collections.Generic;

namespace DaJet.Metadata.Model
{
    public sealed class PredefinedValue
    {
        public Guid Uuid { get; set; } = Guid.Empty;
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public bool IsFolder { get; set; } = false;
        public string Description { get; set; } = string.Empty;
        public List<PredefinedValue> Children { get; set; } = new List<PredefinedValue>();
    }
}