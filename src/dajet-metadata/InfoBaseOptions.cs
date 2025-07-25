using DaJet.Data;

namespace DaJet.Metadata
{
    public sealed class InfoBaseOptions
    {
        public string CacheKey { get; set; } = string.Empty;
        public bool UseExtensions { get; set; } = false;
        public string ConnectionString { get; set; } = string.Empty;
        public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.SqlServer;
    }
}