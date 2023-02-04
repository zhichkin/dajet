using DaJet.Data;
using DaJet.Data.Mapping;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using System.Xml.Linq;

namespace DaJet.Scripting
{
    public static class DataMapper
    {
        public static UnionType GetUnionType(in MetadataProperty property)
        {
            UnionType union = property.PropertyType.GetUnionType();

            foreach (MetadataColumn column in property.Columns)
            {
                if (column.Purpose == ColumnPurpose.Tag)
                {
                    union.UseTag = true;
                    
                    break;
                }
            }

            return union;
        }
        public static PropertyMap CreatePropertyMap(in MetadataProperty property, string alias)
        {
            PropertyMap map = new()
            {
                Name = alias
            };

            UnionType union = GetUnionType(in property);

            map.DataType.Merge(union);



            return map;
        }
        public static ColumnMap CreateColumnMap(in MetadataColumn field, string alias)
        {
            ColumnMap map = new()
            {
                Name = field.Name,
                Alias = alias,
                Type = (UnionTag)(int)field.Purpose
            };

            return map;
        }

        public static ColumnMap CreateColumnMap(UnionTag tag, string name)
        {
            ColumnMap column = new()
            {
                Type = tag,
                Name = name
            };

            return column;
        }
        public static PropertyMap CreatePropertyMap(in SyntaxNode node)
        {
            PropertyMap map = new();
            Configure(in node, in map);
            
            UnionType type = DataTypeInferencer.Infer(in node);
            map.DataType.Merge(type);

            if (type.UseTag)
            {
                map.ToColumn(CreateColumnMap(UnionTag.Tag, map.Name + "_TYPE"));
            }
            
            if (type.IsBoolean)
            {
                map.ToColumn(CreateColumnMap(UnionTag.Boolean, map.Name + "_L"));
            }

            //TODO !!!

            return new PropertyMap();
        }
        private static void Configure(in SyntaxNode node, in PropertyMap property)
        {
            if (node is ColumnExpression column)
            {
                Configure(in column, in property);
            }
            else if (node is ColumnReference reference)
            {
                Configure(in reference, in property);
            }
            else if (node is ScalarExpression scalar)
            {
                Configure(in scalar, in property);
            }
            else if (node is VariableReference variable)
            {
                Configure(in variable, in property);
            }
            else if (node is CaseExpression expression)
            {
                Configure(in expression, in property);
            }
            else if (node is FunctionExpression function)
            {
                Configure(in function, in property);
            }
        }
        private static void Configure(in ColumnExpression column, in PropertyMap property)
        {
            if (!string.IsNullOrEmpty(column.Alias))
            {
                property.Name = column.Alias;
            }
            else
            {
                Configure(column.Expression, in property);
            }
        }
        private static void Configure(in ColumnReference column, in PropertyMap property)
        {
            if (column.Tag is ColumnExpression parent)
            {
                Configure(in parent, in property);
            }
            else if (column.Tag is MetadataProperty source)
            {
                Configure(in source, in property);
            }
        }
        private static void Configure(in MetadataProperty source, in PropertyMap property)
        {
            property.Name = source.Name;
        }
        private static void Configure(in ScalarExpression scalar, in PropertyMap property)
        {

        }
        private static void Configure(in VariableReference variable, in PropertyMap property)
        {

        }
        private static void Configure(in CaseExpression expression, in PropertyMap property)
        {

        }
        private static void Configure(in FunctionExpression function, in PropertyMap property)
        {

        }
    }
}