using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DaJet.Data
{
    public interface IDataSource
    {
        void Create(EntityObject entity);
        void Update(EntityObject entity);
        void Delete(Entity entity);
        EntityObject Select(Entity entity);
        IEnumerable Select(int typeCode);
        IEnumerable Select(int typeCode, Entity owner);
    }
    public interface IAsyncDataSource
    {
        Task CreateAsync(EntityObject entity);
        Task UpdateAsync(EntityObject entity);
        Task DeleteAsync(Entity entity);
        Task<EntityObject> SelectAsync(Entity entity);
        Task<T> SelectAsync<T>(Guid identity) where T : EntityObject;
        Task<T> SelectAsync<T>(Entity entity) where T : EntityObject;
        Task<IEnumerable<T>> QueryAsync<T>() where T : EntityObject;
        Task<IEnumerable<T>> QueryAsync<T>(Entity owner) where T : EntityObject;
    }
    public interface IDataMapper
    {
        void Insert(EntityObject entity);
        void Update(EntityObject entity);
        void Delete(Entity entity);
        EntityObject Select(Guid idenity);
        IEnumerable Select();
        IEnumerable Select(Entity owner);
    }
}