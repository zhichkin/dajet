using DaJet.Data;

namespace DaJet.Metadata
{
    public sealed class MetadataCacheOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
        public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.SqlServer;
    }
}