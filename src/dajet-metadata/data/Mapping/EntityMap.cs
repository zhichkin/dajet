using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;

namespace DaJet.Data.Mapping
{
    public sealed class EntityMap
    {
        public EntityMap() { }
        public int YearOffset { get; set; } = 0;
        public List<PropertyMap> Properties { get; } = new();
        public PropertyMap MapProperty(PropertyMap property)
        {
            property.YearOffset = YearOffset;

            Properties.Add(property);
            
            return property;
        }
        public void Map(in IDbCommand command)
        {
            // TODO
        }
        public Dictionary<string, object> Map(in IDataReader reader)
        {
            Dictionary<string, object> entity = new();

            foreach (PropertyMap property in Properties)
            {
                entity.Add(property.Name, property.GetValue(in reader)!);
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