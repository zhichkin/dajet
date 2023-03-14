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
                Ref = Guid.Empty,
                Code = 0,
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
        private void GetProperties(in TypeDef type, in List<PropertyDef> properties)
        {
            if (type is null) { return; }

            if (type.BaseType is not null)
            {
                GetProperties(type.BaseType, in properties);
            }

            properties.AddRange(type.Properties);
        }
    }
}