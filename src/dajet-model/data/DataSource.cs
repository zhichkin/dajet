using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DaJet.Data
{
    public sealed class DataSourceOptions
    {
        public string ConnectionString { get; set; }
    }
    public interface IDataSource
    {
        void Create(EntityObject entity);
        void Select(EntityObject entity);
        void Update(EntityObject entity);
        void Delete(EntityObject entity);
        List<EntityObject> Select(QueryObject query);
        Task CreateAsync(EntityObject entity);
        Task SelectAsync(EntityObject entity);
        Task UpdateAsync(EntityObject entity);
        Task DeleteAsync(EntityObject entity);
        
        Task<EntityObject> SelectAsync(Entity entity);
        Task<EntityObject> SelectAsync(Type type, Guid uuid);
        Task<List<EntityObject>> SelectAsync(QueryObject query);
    }
    public sealed class QueryObject
    {
        public string Query { get; set; }
        public string Script { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }
    public interface IDataMapper
    {
        void Select(EntityObject entity);
        void Insert(EntityObject entity);
        void Update(EntityObject entity);
        void Delete(EntityObject entity);
    }
}