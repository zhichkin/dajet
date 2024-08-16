using DaJet.Metadata.Model;

namespace DaJet.Data
{
    public interface IDbConfigurator
    {
        bool TryConfigureDatabase(out string error);
        bool TryCreateType(in UserDefinedType type);
        UserDefinedType GetTypeDefinition(in string identifier);
        TableDefinition GetTableDefinition(in string identifier);
    }
}