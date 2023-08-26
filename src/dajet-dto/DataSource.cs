namespace DaJet.Model
{
    public interface IDataSource
    {
        void Create(IPersistent entity);
        void Select(IPersistent entity);
        void Update(IPersistent entity);
        void Delete(IPersistent entity);
        EntityObject Select(EntityObject entity);
        List<TEntity> Select<TEntity>(QueryObject query) where TEntity : IPersistent;
    }
    public sealed class QueryObject
    {
        public string Query { get; set; }
        public string Script { get; set; }
        public Dictionary<string, object> Parameters { get; } = new();
    }
    public interface IDataMapper
    {
        void Select(IPersistent entity);
        void Insert(IPersistent entity);
        void Update(IPersistent entity);
        void Delete(IPersistent entity);
    }
}