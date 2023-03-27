using DaJet.Data;
using System;
using System.Collections.Generic;

namespace DaJet.Model
{
    public sealed class TypeDef
    {
        public Entity Ref { get; set; } = new Entity(SystemTypeCode.TypeDef, Guid.NewGuid()); // self reference
        public string Name { get; set; } = string.Empty;
        public int Code { get; set; } // database generated
        public bool IsTemplate { get; set; } = true; // abstract class
        public string TableName { get; set; } = string.Empty;
        public Entity BaseType { get; set; } = Entity.Undefined; // TypeDef
        public Entity NestType { get; set; } = Entity.Undefined; // TypeDef
        public override string ToString()
        {
            return $"({Code}) {Name} [{TableName}]";
        }
        #region " Переопределение методов сравнения "
        public override int GetHashCode() { return Ref.GetHashCode(); }
        public override bool Equals(object target)
        {
            if (target is null) { return false; }
            if (target is not TypeDef test) { return false; }
            return (this == test);
        }
        public static bool operator ==(TypeDef left, TypeDef right)
        {
            if (ReferenceEquals(left, right)) { return true; }
            if (left is null || right is null) { return false; }
            return left.Ref == right.Ref;
        }
        public static bool operator !=(TypeDef left, TypeDef right)
        {
            return !(left == right);
        }
        #endregion
        public bool IsEntity
        {
            get
            {
                return !BaseType.IsUndefined;
            }
        }
        public List<PropertyDef> Properties { get; } = new();
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