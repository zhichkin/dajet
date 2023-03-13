namespace DaJet.Model
{
    public interface IDataMapper
    {
        void Select(IPersistent entity);
        void Insert(IPersistent entity);
        void Update(IPersistent entity);
        void Delete(IPersistent entity);
    }
}