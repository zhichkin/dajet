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
        void Create(Persistent entity);
        void Select(Persistent entity);
        void Update(Persistent entity);
        void Delete(Persistent entity);
        List<EntityObject> Select(QueryObject query);
        Task CreateAsync(Persistent entity);
        Task SelectAsync(Persistent entity);
        Task UpdateAsync(Persistent entity);
        Task DeleteAsync(Persistent entity);
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
        void Select(Persistent entity);
        void Insert(Persistent entity);
        void Update(Persistent entity);
        void Delete(Persistent entity);
    }
}