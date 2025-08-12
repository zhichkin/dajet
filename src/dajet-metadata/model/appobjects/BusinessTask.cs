using System;
using System.Collections.Generic;

namespace DaJet.Metadata.Model
{
    public sealed class BusinessTask : ApplicationObject, IEntityCode, IEntityDescription, ITablePartOwner
    {
        public int CodeLength { get; set; } = 9;
        public CodeType CodeType { get; set; } = CodeType.String;
        public int DescriptionLength { get; set; } = 25;
        public Guid RoutingTable { get; set; } = Guid.Empty;
        public Guid MainRoutingProperty { get; set; } = Guid.Empty;
        public List<TablePart> TableParts { get; set; } = new();
    }
}