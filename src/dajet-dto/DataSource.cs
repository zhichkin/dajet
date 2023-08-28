namespace DaJet.Model
{
    public interface IDataSource
    {
        void Create(IPersistent entity);
        void Select(IPersistent entity);
        void Update(IPersistent entity);
        void Delete(IPersistent entity);
        List<EntityObject> Select(QueryObject query);
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
        void Select(IPersistent entity);
        void Insert(IPersistent entity);
        void Update(IPersistent entity);
        void Delete(IPersistent entity);
    }
    public sealed class DataSourceOptions
    {
        public string ConnectionString { get; set; }
    }
}