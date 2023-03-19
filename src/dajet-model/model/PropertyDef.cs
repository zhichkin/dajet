using DaJet.Data;
using System;

namespace DaJet.Model
{
    public sealed class PropertyDef
    {
        public Entity Ref { get; set; } = new Entity(2, Guid.NewGuid()); // self reference
        public string Name { get; set; }
        public int Code { get; set; } // database generated
        public Entity Owner { get; set; } = Entity.Undefined; // TypeDef
        public int Ordinal { get; set; }
        public UnionType DataType { get; set; }
        public int Qualifier1 { get; set; }
        public int Qualifier2 { get; set; }
        public string ColumnName { get; set; }
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsVersion { get; set; }
        public bool IsIdentity { get; set; }
        public int IdentitySeed { get; set; } = 1;
        public int IdentityIncrement { get; set; } = 1;
        public PropertyDef Copy()
        {
            return new PropertyDef()
            {
                Name = Name,
                ColumnName = ColumnName,
                DataType = DataType?.Copy(),
                Qualifier1 = Qualifier1,
                Qualifier2 = Qualifier2,
                IsVersion = IsVersion,
                IsNullable = IsNullable,
                IsPrimaryKey = IsPrimaryKey,
                IsIdentity = IsIdentity,
                IdentitySeed = IdentitySeed,
                IdentityIncrement = IdentityIncrement
            };
        }
    }
}