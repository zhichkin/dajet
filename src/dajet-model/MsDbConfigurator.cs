using DaJet.Data;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace DaJet.Model
{
    public sealed class MsDbConfigurator : IDbConfigurator
    {
        private readonly IQueryExecutor _executor;
        public MsDbConfigurator(IQueryExecutor executor)
        {
            _executor = executor;
        }
        public void CreateSystemDatabase()
        {
            TypeDef definition = GetTypeDef();

            CreateTable(in definition);
        }
        public static string GetDbTypeName(UnionTag tag)
        {
            if (tag == UnionTag.Tag) { return "binary"; }
            else if (tag == UnionTag.Boolean) { return "binary"; }
            else if (tag == UnionTag.Numeric) { return "numeric"; }
            else if (tag == UnionTag.DateTime) { return "datetime2"; }
            else if (tag == UnionTag.String) { return "nvarchar"; }
            else if (tag == UnionTag.Binary) { return "varbinary"; }
            else if (tag == UnionTag.Uuid) { return "binary"; }
            else if (tag == UnionTag.TypeCode) { return "binary"; }
            else if (tag == UnionTag.Entity) { return "binary"; }
            else if (tag == UnionTag.Version) { return "timestamp"; } // rowversion
            else if (tag == UnionTag.Integer) { return "int"; }

            return "varbinary(max)"; // UnionTag.Undefined
        }
        private void CreateTable(in TypeDef definition)
        {
            StringBuilder script = new();

            script.Append("CREATE TABLE ").AppendLine(definition.TableName).Append('(').AppendLine();

            PropertyDef property;
            List<PropertyDef> properties = definition.GetProperties();

            for (int i = 0; i < properties.Count; i++)
            {
                property = properties[i];

                if (i > 0) { script.Append(',').AppendLine(); }

                script.Append(property.ColumnName);

                UnionTag tag = property.DataType.GetSingleTagOrUndefined();

                script.Append(' ').Append(GetDbTypeName(tag));

                if (property.Qualifier1 > 0)
                {
                    script.Append('(').Append(property.Qualifier1);

                    if (property.Qualifier2 > 0)
                    {
                        script.Append(',').Append(property.Qualifier2);
                    }

                    script.Append(')');
                }

                if (property.IsDbGenerated)
                {
                    //TODO: IsPrimaryKey
                }
                else
                {
                    //TODO: IsIdentity
                }

                script.Append(' ').Append(property.IsNullable ? "NULL" : "NOT NULL");
            }

            script.AppendLine().Append(");");

            string sql = script.ToString();

            _executor.ExecuteNonQuery(in sql, 10);
        }
        private TypeDef GetMetadataType()
        {
            int ordinal = TypeDef.Entity.Properties.Count;

            TypeDef metadata = new()
            {
                Ref = Guid.Empty,
                Code = 0,
                Name = "Metadata",
                BaseType = TypeDef.Entity
            };

            metadata.Properties.Add(new PropertyDef()
            {
                Ref = Guid.Empty,
                Code = 0,
                Name = "Name",
                Owner = metadata,
                Ordinal = ++ordinal,
                ColumnName = "name",
                Qualifier1 = 64,
                DataType = new UnionType() { IsString = true }
            });

            metadata.Properties.Add(new PropertyDef()
            {
                Ref = Guid.Empty,
                Code = 0,
                Name = "Code",
                Owner = metadata,
                Ordinal = ++ordinal,
                ColumnName = "type_code",
                IsDbGenerated = true,
                DataType = new UnionType() { IsInteger = true }
            });

            return metadata;
        }
        private TypeDef GetTypeDef()
        {
            TypeDef metadata = GetMetadataType();
            int ordinal = metadata.Properties.Count + metadata.BaseType.Properties.Count;

            TypeDef definition = new()
            {
                Ref = Guid.NewGuid(),
                Code = 1,
                Name = "TypeDef",
                TableName = "md_types",
                BaseType = metadata
            };

            definition.Properties.Add(new PropertyDef()
            {
                Ref = Guid.NewGuid(),
                Name = "TableName",
                Owner = definition,
                Ordinal = ++ordinal,
                ColumnName = "table_name",
                Qualifier1 = 32,
                DataType = new UnionType() { IsString = true }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Ref = Guid.NewGuid(),
                Name = "BaseType",
                Owner = definition,
                Ordinal = ++ordinal,
                ColumnName = "base_type",
                DataType = new UnionType() { IsEntity = true, TypeCode = definition.Code }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Ref = Guid.NewGuid(),
                Name = "NestType",
                Owner = definition,
                Ordinal = ++ordinal,
                ColumnName = "nest_type",
                DataType = new UnionType() { IsEntity = true, TypeCode = definition.Code }
            });

            return definition;
        }
        private void GetRelations(in PropertyDef property)
        {
            //UnionType type = property.DataType;

            //if (type.IsEntity)
            //{
            //    List<Relation> relations = new();
            //    Entity source = new(property.Code, property.Ref);

            //    foreach (TypeDef target in references)
            //    {
            //        relations.Add(new Relation() { Source = source, Target = target });
            //    }
            //}
        }
    }
}