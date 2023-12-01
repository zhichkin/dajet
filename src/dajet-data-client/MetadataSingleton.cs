using DaJet.Metadata;

namespace DaJet.Data.Client
{
    internal static class MetadataSingleton
    {
        private static readonly MetadataService _metadata;
        static MetadataSingleton()
        {
            _metadata = new();
        }
        internal static IMetadataService Instance { get { return _metadata; } }
    }
}