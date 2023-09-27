using System;
using System.Collections;
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

        Task CreateAsync(EntityObject entity);
        Task UpdateAsync(EntityObject entity);
        Task DeleteAsync(EntityObject entity);
        Task<EntityObject> SelectAsync(Entity entity);
        Task<IEnumerable> SelectAsync(int typeCode, string propertyName, Entity value);
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