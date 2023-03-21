using DaJet.Model;

namespace DaJet.Data
{
    public interface IDbConfigurator
    {
        void CreateDatabase();
        void CreateUserType(in TypeDef definition);
    }
}