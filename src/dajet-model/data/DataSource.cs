using System.Collections;
using System.Threading.Tasks;

namespace DaJet.Data
{
    public interface IDataSource
    {
        void Create(EntityObject entity);
        void Update(EntityObject entity);
        void Delete(Entity entity);
        IEnumerable Select();
        EntityObject Select(Entity entity);
        IEnumerable Select(int typeCode, string propertyName, Entity value);
    }
    public interface IAsyncDataSource
    {
        Task CreateAsync(EntityObject entity);
        Task UpdateAsync(EntityObject entity);
        Task DeleteAsync(Entity entity);
        Task<IEnumerable> SelectAsync();
        Task<EntityObject> SelectAsync(Entity entity);
        Task<IEnumerable> SelectAsync(int typeCode, string propertyName, Entity value);
    }
}