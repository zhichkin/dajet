using DaJet.Data;
using System;
using System.Collections.Generic;

namespace DaJet.Model
{
    public sealed class TypeDef
    {
        public Entity Ref { get; set; } = new Entity(3, Guid.NewGuid());
        public int Code { get; set; } = 3;
        public string Name { get; set; }
        public string TableName { get; set; }
        public Entity BaseType { get; set; } = Entity.Undefined;
        public Entity NestType { get; set; } = Entity.Undefined;
        public List<PropertyDef> Properties { get; } = new();
        public bool IsEntity
        {
            get
            {
                return !BaseType.IsUndefined;
            }
        }
        public List<PropertyDef> GetPrimaryKey()
        {
            List<PropertyDef> columns = new();

            foreach (PropertyDef property in Properties)
            {
                if (property.IsPrimaryKey)
                {
                    columns.Add(property);
                }
            }

            return columns;
        }
    }
}