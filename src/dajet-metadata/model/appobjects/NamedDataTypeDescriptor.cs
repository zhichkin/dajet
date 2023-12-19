namespace DaJet.Metadata.Model
{
    public sealed class NamedDataTypeDescriptor : MetadataObject
    {
        public DataTypeDescriptor DataTypeDescriptor { get; set; }
        public DataTypeDescriptor ExtensionDataTypeDescriptor { get; set; }
    }
}