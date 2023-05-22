using DaJet.Metadata.Model;
using DaJet.Model;

namespace DaJet.Data
{
    public interface IDbConfigurator
    {
        void CreateDatabase();
        int GenerateTypeCode();
        void CreateUserType(in TypeDef definition);
        void CreateProperties(in TypeDef definition); // ?
        void CreateRelations(in TypeDef definition); // ?
        void CreateTableOfType(in string identifier, in string tableName);

        TypeDefinition GetTypeDefinition(in string[] identifiers);
    }
}