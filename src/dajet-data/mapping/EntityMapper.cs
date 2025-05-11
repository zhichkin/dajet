using System.Data;
using System.Reflection;

namespace DaJet.Data
{
    public sealed class EntityMapper : IEntityMapper
    {
        private int ordinal = -1;
        public EntityMapper() { }
        public string Name { get; set; } //NOTE: SELECT, CONSUME, DELETE OUTPUT : table name if present
        public int YearOffset { get; set; } = 0;
        public List<PropertyMapper> Properties { get; } = new();
        public void Add(PropertyMapper mapper)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            AddPropertyMapper(mapper.Name, mapper.DataType);
        }
        public void AddPropertyMapper(in string name, in UnionType type)
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
        public void Map(in IDataReader reader, in DataObject record)
        {
            PropertyMapper property;

            int count = Properties.Count;

            for (int i = 0; i < count; i++)
            {
                property = Properties[i];

                if (property is not null)
                {
                    record.SetValue(property.Name, property.GetValue(in reader));
                }
            }
        }
        public Dictionary<string, object> Map(in IDataReader reader)
        {
            Dictionary<string, object> record = new();

            foreach (PropertyMapper property in Properties)
            {
                record.Add(property.Name, property.GetValue(in reader));
            }

            return record;
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
        public void Map<TEntity>(in IDataReader reader, in TEntity entity) where TEntity : class
        {
            object value;

            PropertyInfo property;
            
            Type type = typeof(TEntity);

            foreach (PropertyMapper map in Properties)
            {
                property = type.GetProperty(map.Name);

                if (property is null)
                {
                    continue;
                }

                value = map.GetValue(in reader);

                property.SetValue(entity, value);
            }
        }
    }
}