using System;
using System.Collections.Generic;

namespace DaJet.Metadata.Model
{
    public sealed class BusinessProcess : ApplicationObject, ITablePartOwner
    {
        public int NumberLength { get; set; } = 8;
        public NumberType NumberType { get; set; } = NumberType.String;
        public Periodicity Periodicity { get; set; } = Periodicity.None;
        public Guid BusinessTask { get; set; } = Guid.Empty;
        public List<TablePart> TableParts { get; set; } = new();
    }
}