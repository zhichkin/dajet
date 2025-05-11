using System.Data;

namespace DaJet.Data
{
    public interface IEntityMapper
    {
        public List<PropertyMapper> Properties { get; }
        public void Add(PropertyMapper mapper);
        public void Map(in IDataReader reader, in DataObject record);
    }
}