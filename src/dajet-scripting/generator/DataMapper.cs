using DaJet.Data;
using DaJet.Data.Mapping;
using DaJet.Metadata.Model;

namespace DaJet.Scripting
{
    internal static class DataMapper
    {
        public static PropertyMap CreatePropertyMap(in MetadataProperty property, string alias)
        {
            PropertyMap map = new()
            {
                Name = alias
            };

            DataTypeSet type = property.PropertyType;

            if (type.IsUuid)
            {
                map.Type = typeof(Guid);
            }
            else if (type.IsBinary || type.IsValueStorage)
            {
                map.Type = typeof(byte[]);
            }
            else if (type.IsMultipleType)
            {
                map.Type = typeof(Union);
                map.TypeCode = type.TypeCode;
            }
            else if (type.CanBeBoolean)
            {
                map.Type = typeof(bool);
            }
            else if (type.CanBeNumeric)
            {
                map.Type = typeof(decimal);
            }
            else if (type.CanBeDateTime)
            {
                map.Type = typeof(DateTime);
            }
            else if (type.CanBeString)
            {
                map.Type = typeof(string);
            }
            else if (type.CanBeReference)
            {
                map.Type = typeof(EntityRef);
                map.TypeCode = type.TypeCode;
            }

            return map;
        }
        public static ColumnMap CreateColumnMap(in MetadataColumn field, string alias)
        {
            ColumnMap map = new()
            {
                Name = field.Name,
                Alias = alias,
                Purpose = (ColumnPurpose)(int)field.Purpose
            };

            return map;
        }
    }
}