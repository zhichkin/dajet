using DaJet.Metadata.Model;

namespace DaJet.Data
{
    public interface IDbConfigurator
    {
        bool TryCreateType(in TypeDefinition type);
        TypeDefinition GetTypeDefinition(in string identifier);
    }
}