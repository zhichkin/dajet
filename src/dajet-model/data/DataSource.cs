using DaJet.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DaJet.Data
{
    public interface IDataSource
    {
        IDomainModel Model { get; }
        string ConnectionString { get; }
        void Create(EntityObject entity);
        void Update(EntityObject entity);
        void Delete(Entity entity);
        void Delete<T>(Guid identity) where T : EntityObject;
        EntityObject Select(Entity entity);
        T Select<T>(int code) where T : EntityObject;
        T Select<T>(string name) where T : EntityObject;
        T Select<T>(Guid identity) where T : EntityObject;
        T Select<T>(Entity entity) where T : EntityObject;
        T Select<T>(Entity owner, string name) where T : EntityObject;
        IEnumerable Select(int typeCode);
        IEnumerable Select(int typeCode, Entity owner);
        IEnumerable<T> Query<T>() where T : EntityObject;
        IEnumerable<T> Query<T>(Entity owner) where T : EntityObject;
        List<DataObject> Query(string query, Dictionary<string, object> parameters);
    }
    public interface IAsyncDataSource
    {
        IDomainModel Model { get; }
        Task CreateAsync(EntityObject entity);
        Task UpdateAsync(EntityObject entity);
        Task DeleteAsync(Entity entity);
        Task<EntityObject> SelectAsync(Entity entity);
        Task<T> SelectAsync<T>(string name) where T : EntityObject;
        Task<T> SelectAsync<T>(Guid identity) where T : EntityObject;
        Task<T> SelectAsync<T>(Entity entity) where T : EntityObject;
        Task<IEnumerable<T>> QueryAsync<T>() where T : EntityObject;
        Task<IEnumerable<T>> QueryAsync<T>(Entity owner) where T : EntityObject;
        Task<List<DataObject>> QueryAsync(string query, Dictionary<string, object> parameters);
    }
    public interface IDataMapper
    {
        void Insert(EntityObject entity);
        void Update(EntityObject entity);
        void Delete(Entity entity);
        EntityObject Select(int code);
        EntityObject Select(string name);
        EntityObject Select(Guid idenity);
        EntityObject Select(Entity owner, string name);
        IEnumerable Select();
        IEnumerable Select(Entity owner);
    }
}