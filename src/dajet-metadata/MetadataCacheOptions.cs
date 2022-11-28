using DaJet.Data;
using DaJet.Metadata.Extensions;

namespace DaJet.Metadata
{
    public sealed class MetadataCacheOptions
    {
        public ExtensionInfo Extension { get; set; } = null;
        public string ConnectionString { get; set; } = string.Empty;
        public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.SqlServer;
    }
}