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
    }
}