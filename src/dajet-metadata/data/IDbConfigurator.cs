using DaJet.Metadata.Model;

namespace DaJet.Data
{
    public interface IDbConfigurator
    {
        bool TryCreateType(in UserDefinedType type);
        UserDefinedType GetTypeDefinition(in string identifier);
    }
}