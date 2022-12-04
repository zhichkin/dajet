using System;

namespace DaJet.Metadata.Model
{
    public sealed class NamedDataTypeSet : MetadataObject
    {
        public DataTypeSet DataTypeSet { get; set; }
        public DataTypeSet ExtensionDataTypeSet { get; set; }
    }
}