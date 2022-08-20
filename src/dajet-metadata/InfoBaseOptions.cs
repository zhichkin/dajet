using DaJet.Data;

namespace DaJet.Metadata
{
    public sealed class InfoBaseOptions
    {
        public string Key { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.SqlServer;
    }
}