using DaJet.Data;
using System.Data;
using System.Reflection;

namespace DaJet.Scripting
{
    public sealed class EntityMap
    {
        private int ordinal = -1;
        public EntityMap() { }
        public int YearOffset { get; set; } = 0;
        public List<PropertyMap> Properties { get; } = new();
        public void Map(in string name, in UnionType type)
        {
            PropertyMap property = new()
            {
                Name = name,
                YearOffset = YearOffset
            };
            property.DataType.Merge(type);

            List<UnionTag> columns = type.ToColumnList();

            for (int i = 0; i < columns.Count; i++)
            {
                property.Columns.Add(columns[i], new ColumnMap()
                {
                    Type = columns[i],
                    Ordinal = ++ordinal
                });
            }

            Properties.Add(property);
        }
        public void Map(in IDbCommand command)
        {
            //TODO: map IDbCommand for DML
        }
        public Dictionary<string, object> Map(in IDataReader reader)
        {
            Dictionary<string, object> entity = new();

            foreach (PropertyMap property in Properties)
            {
                entity.Add(property.Name, property.GetValue(in reader));
            }

            return entity;
        }
        public TEntity Map<TEntity>(in IDataReader reader) where TEntity : class, new()
        {
            TEntity entity = new();

            object value;
            PropertyInfo property;
            Type type = typeof(TEntity);

            foreach (PropertyMap map in Properties)
            {
                property = type.GetProperty(map.Name);

                if (property == null)
                {
                    continue;
                }

                value = map.GetValue(in reader);

                property.SetValue(entity, value);
            }

            return entity;
        }
        public void Map<TEntity>(in IDataReader reader, in TEntity entity) where TEntity : class, new()
        {
            object value;
            PropertyInfo property;
            Type type = typeof(TEntity);

            foreach (PropertyMap map in Properties)
            {
                property = type.GetProperty(map.Name);

                if (property == null)
                {
                    continue;
                }

                value = map.GetValue(in reader);

                property.SetValue(entity, value);
            }
        }
    }
}