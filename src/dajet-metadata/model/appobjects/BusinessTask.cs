using System;
using System.Collections.Generic;

namespace DaJet.Metadata.Model
{
    public sealed class BusinessTask : ApplicationObject, IEntityDescription, ITablePartOwner
    {
        public int NumberLength { get; set; } = 8;
        public NumberType NumberType { get; set; } = NumberType.String;
        public int DescriptionLength { get; set; } = 25;
        public Guid RoutingTable { get; set; } = Guid.Empty;
        public Guid MainRoutingProperty { get; set; } = Guid.Empty;
        public List<TablePart> TableParts { get; set; } = new();
    }
}