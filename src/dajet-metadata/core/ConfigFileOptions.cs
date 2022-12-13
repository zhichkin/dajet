using DaJet.Data;
using System;

namespace DaJet.Metadata.Core
{
    public sealed class ConfigFileOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
        public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.SqlServer;
        public bool IsExtension { get; set; } = false;
        public string TableName { get; set; } = ConfigTables.Config;
        public string FileName { get; set; } = ConfigFiles.Root;
        public Guid MetadataUuid { get; set; } = Guid.Empty;
    }
}