using DaJet.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace DaJet.Model
{
    public sealed class MsDbConfigurator : IDbConfigurator
    {
        private readonly TypeDef ENTITY;
        private readonly TypeDef METADATA;
        private readonly TypeDef TYPE_DEF;
        
        private readonly IQueryExecutor _executor;
        public MsDbConfigurator(IQueryExecutor executor)
        {
            _executor = executor;

            ENTITY = CreateEntity();
            METADATA = CreateMetadataType();
            TYPE_DEF = CreateTypeDefinition();
        }

        public void CreateDatabase()
        {
            List<string> sql = new()
            {
                BuildCreateTableScript(in TYPE_DEF),
                BuildCreateIndexScript(in TYPE_DEF)
            };

            _executor.TxExecuteNonQuery(in sql, 10);

            CreateTypeDef(in ENTITY);
            CreateTypeDef(in METADATA);
            CreateTypeDef(in TYPE_DEF);
        }
        private TypeDef CreateEntity()
        {
            TypeDef entity = new()
            {
                Name = "ENTITY", Ref = new Entity(1, new Guid("B34412E3-B9BA-46B4-887D-961204543E91"))
            };

            entity.Properties.Add(new PropertyDef()
            {
                Name = "Ref",
                Owner = entity.Ref,
                Ordinal = 1,
                ColumnName = "type_ref",
                IsPrimaryKey = true,
                DataType = new UnionType() { IsEntity = true, TypeCode = entity.Ref.TypeCode }
            });

            return entity;
        }
        private TypeDef CreateMetadataType()
        {
            int ordinal = ENTITY.Properties.Count;

            TypeDef metadata = new()
            {
                Name = "METADATA", BaseType = ENTITY.Ref, Ref = new Entity(1, new Guid("43ED3777-C00B-4E45-9D8B-5783771C184B"))
            };

            metadata.Properties.Add(new PropertyDef()
            {
                Name = "Name",
                Owner = metadata.Ref,
                Ordinal = ++ordinal,
                ColumnName = "type_name",
                Qualifier1 = 64,
                DataType = new UnionType() { IsString = true }
            });

            metadata.Properties.Add(new PropertyDef()
            {
                Name = "Code",
                Owner = metadata.Ref,
                Ordinal = ++ordinal,
                ColumnName = "type_code",
                IsIdentity = true,
                DataType = new UnionType() { IsInteger = true }
            });

            return metadata;
        }
        private TypeDef CreateTypeDefinition()
        {
            TypeDef definition = new()
            {
                Name = "TypeDef",
                BaseType = METADATA.Ref,
                TableName = "dajet_types",
                Ref = new Entity(1, new Guid("B910A137-D045-4C74-9BF5-D8C781F36C2C"))
            };

            // Properties derived from ENTITY

            foreach (PropertyDef property in ENTITY.Properties)
            {
                PropertyDef new_prop = property.Copy();
                new_prop.Owner = definition.Ref;
                definition.Properties.Add(new_prop);
            }

            // Properties derived from METADATA

            foreach (PropertyDef property in METADATA.Properties)
            {
                PropertyDef new_prop = property.Copy();
                new_prop.Owner = definition.Ref;
                definition.Properties.Add(new_prop);
            }

            int ordinal = ENTITY.Properties.Count + METADATA.Properties.Count;

            // TypeDef class own properties

            definition.Properties.Add(new PropertyDef()
            {
                Name = "TableName",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "table_name",
                Qualifier1 = 32,
                DataType = new UnionType() { IsString = true }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Name = "BaseType",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "base_type",
                DataType = new UnionType() { IsEntity = true, TypeCode = definition.Ref.TypeCode }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Name = "NestType",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "nest_type",
                DataType = new UnionType() { IsEntity = true, TypeCode = definition.Ref.TypeCode }
            });

            return definition;
        }
        private TypeDef CreatePropertyDefinition()
        {
            //TODO: PropertyDef database table
            throw new NotImplementedException();
        }
        private TypeDef CreateRelationDefinition()
        {
            //TODO: Relation database table
            throw new NotImplementedException();
        }

        public static string GetDbTypeName(UnionTag tag)
        {
            if (tag == UnionTag.Tag) { return "binary"; }
            else if (tag == UnionTag.Boolean) { return "binary(1)"; }
            else if (tag == UnionTag.Numeric) { return "numeric"; }
            else if (tag == UnionTag.DateTime) { return "datetime2"; }
            else if (tag == UnionTag.String) { return "nvarchar"; }
            else if (tag == UnionTag.Binary) { return "varbinary"; }
            else if (tag == UnionTag.Uuid) { return "binary(16)"; }
            else if (tag == UnionTag.TypeCode) { return "binary(4)"; }
            else if (tag == UnionTag.Entity) { return "binary(16)"; }
            else if (tag == UnionTag.Version) { return "rowversion"; }
            else if (tag == UnionTag.Integer) { return "int"; }

            return "varbinary(max)"; // UnionTag.Undefined
        }
        private string BuildCreateTableScript(in TypeDef definition)
        {
            StringBuilder script = new();

            script.Append("CREATE TABLE ").AppendLine(definition.TableName).Append('(').AppendLine();

            PropertyDef property;
            List<PropertyDef> properties = definition.Properties;

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

                if (property.IsIdentity)
                {
                    script.Append(' ')
                        .Append("IDENTITY(")
                        .Append(property.IdentitySeed)
                        .Append(',')
                        .Append(property.IdentityIncrement)
                        .Append(')');
                }

                script.Append(' ').Append(property.IsNullable ? "NULL" : "NOT NULL");
            }

            script.AppendLine().Append(");");

            return script.ToString();
        }
        private string BuildCreateIndexScript(in TypeDef definition)
        {
            StringBuilder script = new();

            script.Append("CREATE UNIQUE CLUSTERED INDEX pk_").Append(definition.TableName);
            script.Append(" ON ").Append(definition.TableName).Append('(');

            PropertyDef property;
            List<PropertyDef> properties = definition.GetPrimaryKey();

            for (int i = 0; i < properties.Count; i++)
            {
                property = properties[i];

                if (i > 0) { script.Append(','); }

                script.Append(property.ColumnName).Append(" ASC");
            }

            script.Append(");");

            return script.ToString();
        }
        
        private void CreateTypeDef(in TypeDef definition)
        {
            string script = new StringBuilder()
                .Append("INSERT dajet_types(type_ref, type_name, table_name, base_type, nest_type)")
                .AppendLine()
                .Append("OUTPUT INSERTED.type_code AS type_code")
                .AppendLine()
                .Append("SELECT @type_ref, @type_name, @table_name, @base_type, @nest_type")
                .ToString();

            Dictionary<string, object> parameters = new()
            {
                { "type_ref", definition.Ref.Identity.ToByteArray() },
                { "type_name", definition.Name is null ? string.Empty : definition.Name },
                { "table_name", definition.TableName is null ? string.Empty : definition.TableName },
                { "base_type", definition.BaseType.Identity.ToByteArray() },
                { "nest_type", definition.NestType.Identity.ToByteArray() }
            };

            foreach (IDataReader reader in _executor.ExecuteReader(script, 10, parameters))
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.GetName(i) == "type_code")
                    {
                        definition.Code = reader.GetInt32(i); break;
                    }
                }
            }
        }
        public TypeDef SelectTypeDef(in string identifier)
        {
            TypeDef definition = new();

            string script = new StringBuilder()
                .Append("SELECT type_ref, type_name, type_code, table_name, base_type, nest_type")
                .AppendLine()
                .Append("FROM dajet_types")
                .AppendLine()
                .Append("WHERE type_name = @name")
                .ToString();

            Dictionary<string, object> parameters = new()
            {
                { "name", identifier }
            };

            foreach (IDataReader reader in _executor.ExecuteReader(script, 10, parameters))
            {
                definition.Ref = new Entity(definition.Ref.TypeCode, new Guid((byte[])reader["type_ref"]));
                definition.Name = (string)reader["type_name"];
                definition.Code = (int)reader["type_code"];
                definition.TableName = (string)reader["table_name"];
                definition.BaseType = new Entity(definition.Ref.TypeCode, new Guid((byte[])reader["base_type"]));
                definition.NestType = new Entity(definition.Ref.TypeCode, new Guid((byte[])reader["nest_type"]));
            }

            //TODO: SELECT properties FROM dajet_props WHERE owner = definition.Ref
            definition.Properties.AddRange(TYPE_DEF.Properties);

            return definition;
        }

        private string BuildCreateTypeScript(in TypeDef definition)
        {
            StringBuilder script = new();
            StringBuilder insert = new();
            StringBuilder select = new();

            insert.Append("INSERT md_types (_ref, name, table_name, base_type, nest_type)");
            select.Append("SELECT ");

            PropertyDef property;
            List<PropertyDef> properties = definition.Properties;

            string versionColumn = string.Empty;
            string identityColumn = string.Empty;
            
            for (int i = 0; i < properties.Count; i++)
            {
                property = properties[i];

                if (property.IsVersion) { versionColumn = property.ColumnName; continue; }
                if (property.IsIdentity) { identityColumn = property.ColumnName; continue; }

                if (i > 0) { insert.Append(','); select.Append(','); }

                insert.Append(property.ColumnName);
                select.Append('@').Append(property.ColumnName);
            }
            insert.Append(')');
            select.Append(';');

            script.Append(insert);

            if (string.IsNullOrEmpty(versionColumn) && string.IsNullOrEmpty(identityColumn))
            {
                return script.AppendLine().Append(select).ToString();
            }

            script.AppendLine().Append("OUTPUT ");

            if (!string.IsNullOrEmpty(identityColumn))
            {
                script.Append("INSERTED.").Append(identityColumn).Append(" AS ").Append(identityColumn);
            }

            if (!string.IsNullOrEmpty(versionColumn))
            {
                if (!string.IsNullOrEmpty(identityColumn)) { script.Append(','); }

                script.Append("INSERTED.").Append(versionColumn).Append(" AS ").Append(identityColumn);
            }
            
            return script.AppendLine().Append(select).ToString();
        }
        private string BuildCreatePropertyScript(in TypeDef definition)
        {
            StringBuilder script = new();
            StringBuilder insert = new();
            StringBuilder select = new();

            insert.Append("INSERT md_properties (_ref");
            select.Append("SELECT ");

            PropertyDef property;
            List<PropertyDef> properties = definition.Properties;

            string versionColumn = string.Empty;
            string identityColumn = string.Empty;

            for (int i = 0; i < properties.Count; i++)
            {
                property = properties[i];

                if (property.IsVersion) { versionColumn = property.ColumnName; continue; }
                if (property.IsIdentity) { identityColumn = property.ColumnName; continue; }

                if (i > 0) { insert.Append(','); select.Append(','); }

                insert.Append(property.ColumnName);
                select.Append('@').Append(property.ColumnName);
            }
            insert.Append(')');
            select.Append(';');

            script.Append(insert);

            if (string.IsNullOrEmpty(versionColumn) && string.IsNullOrEmpty(identityColumn))
            {
                return script.AppendLine().Append(select).ToString();
            }

            script.AppendLine().Append("OUTPUT ");

            if (!string.IsNullOrEmpty(identityColumn))
            {
                script.Append("INSERTED.").Append(identityColumn).Append(" AS ").Append(identityColumn);
            }

            if (!string.IsNullOrEmpty(versionColumn))
            {
                if (!string.IsNullOrEmpty(identityColumn)) { script.Append(','); }

                script.Append("INSERTED.").Append(versionColumn).Append(" AS ").Append(identityColumn);
            }

            return script.AppendLine().Append(select).ToString();
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