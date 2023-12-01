using System.Data;
using System.Dynamic;
using System.Reflection;

namespace DaJet.Data
{
    public sealed class EntityMapper
    {
        private int ordinal = -1;
        public EntityMapper() { }
        public int YearOffset { get; set; } = 0;
        public List<PropertyMapper> Properties { get; } = new();
        public void Map(in string name, in UnionType type)
        {
            PropertyMapper property = new()
            {
                Name = name,
                YearOffset = YearOffset
            };
            property.DataType.Merge(type);

            List<UnionTag> columns = type.ToColumnList();

            for (int i = 0; i < columns.Count; i++)
            {
                property.Columns.Add(columns[i], new ColumnMapper()
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
        public void Map(in IDataReader reader, out dynamic entity)
        {
            entity = new ExpandoObject();

            IDictionary<string, object> bag = entity;

            foreach (PropertyMapper property in Properties)
            {
                bag.Add(property.Name, property.GetValue(in reader));
            }
        }
        public void Map(in IDataReader reader, out IDataRecord record)
        {
            DataRecord data = new();

            foreach (PropertyMapper property in Properties)
            {
                data.SetValue(property.Name, property.GetValue(in reader));
            }

            record = data;
        }
        public void Map(in IDataReader reader, in DataRecord record)
        {
            foreach (PropertyMapper property in Properties)
            {
                record.SetValue(property.Name, property.GetValue(in reader));
            }
        }
        public Dictionary<string, object> Map(in IDataReader reader)
        {
            Dictionary<string, object> entity = new();

            foreach (PropertyMapper property in Properties)
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

            foreach (PropertyMapper map in Properties)
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

            foreach (PropertyMapper map in Properties)
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