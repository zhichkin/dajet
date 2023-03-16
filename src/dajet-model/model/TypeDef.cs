using DaJet.Data;
using System;
using System.Collections.Generic;

namespace DaJet.Model
{
    public sealed class TypeDef
    {
        private static readonly TypeDef ENTITY;
        static TypeDef()
        {
            ENTITY = new()
            {
                Ref = Guid.NewGuid(),
                Code = 1,
                Name = "ENTITY"
            };

            ENTITY.Properties.Add(new PropertyDef()
            {
                Ref = Guid.Empty,
                Code = 0,
                Name = "Ref",
                Owner = ENTITY,
                Ordinal = 1,
                ColumnName = "_ref",
                IsPrimaryKey = true,
                Qualifier1 = 16,
                DataType = new UnionType() { IsUuid = true }
            });
        }
        public static TypeDef Entity { get { return ENTITY; } }
        public Guid Ref { get; set; }
        public int Code { get; set; }
        public string Name { get; set; }
        public string TableName { get; set; }
        public TypeDef BaseType { get; set; }
        public TypeDef NestType { get; set; }
        public List<PropertyDef> Properties { get; } = new();
        public bool IsEntity
        {
            get
            {
                if (this == ENTITY) {return true; }

                TypeDef parent = BaseType;

                while (parent is not null)
                {
                    if (parent == ENTITY)
                    {
                        return true;
                    }
                    
                    parent = parent.BaseType;
                }

                return false;
            }
        }
        public List<PropertyDef> GetProperties()
        {
            List<PropertyDef> properties = new();

            GetProperties(this, in properties);

            return properties;
        }
        private void GetProperties(in TypeDef definition, in List<PropertyDef> properties)
        {
            if (definition is null) { return; }

            if (definition.BaseType is not null)
            {
                GetProperties(definition.BaseType, in properties);
            }

            properties.AddRange(definition.Properties);
        }
        public List<PropertyDef> GetPrimaryKey()
        {
            List<PropertyDef> columns = new();

            GetPrimaryKey(this, in columns);

            return columns;
        }
        private void GetPrimaryKey(in TypeDef definition, in List<PropertyDef> columns)
        {
            if (definition is null) { return; }

            if (definition.BaseType is not null)
            {
                GetPrimaryKey(definition.BaseType, in columns);
            }

            foreach (PropertyDef property in definition.Properties)
            {
                if (property.IsPrimaryKey)
                {
                    columns.Add(property);
                }
            }
        }
    }
}