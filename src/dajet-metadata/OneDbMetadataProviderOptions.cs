using DaJet.Data;
using DaJet.Metadata.Extensions;

namespace DaJet.Metadata
{
    public sealed class OneDbMetadataProviderOptions
    {
        public bool UseExtensions { get; set; } = false;
        public ExtensionInfo Extension { get; set; } = null;
        public string ConnectionString { get; set; } = string.Empty;
        public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.SqlServer;
        public bool ResolveReferences { get; set; } = false;
    }
}