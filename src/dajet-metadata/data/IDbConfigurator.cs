using DaJet.Metadata.Model;

namespace DaJet.Data
{
    public interface IDbConfigurator
    {
        bool TryCreateType(in EntityDefinition type);
        EntityDefinition GetTypeDefinition(in string identifier);
    }
}