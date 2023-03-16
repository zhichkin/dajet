using DaJet.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace DaJet.Model
{
    public sealed class MsDbConfigurator : IDbConfigurator
    {
        private readonly IQueryExecutor _executor;
        public MsDbConfigurator(IQueryExecutor executor)
        {
            _executor = executor;

            _entity = CreateEntity();
            _metadata = CreateMetadataType();
            _definition = CreateTypeDefinition();
        }
        private TypeDef _entity;
        private TypeDef _metadata;
        private TypeDef _definition;
        private TypeDef CreateEntity()
        {
            TypeDef entity = new()
            {
                Ref = new Entity(1, Guid.NewGuid()),
                Code = 1,
                Name = "ENTITY"
            };

            entity.Properties.Add(new PropertyDef()
            {
                Ref = new Entity(4, Guid.NewGuid()),
                Code = 4,
                Name = "Ref",
                Owner = entity.Ref,
                Ordinal = 1,
                ColumnName = "_ref",
                IsPrimaryKey = true,
                Qualifier1 = 16,
                DataType = new UnionType() { IsEntity = true, TypeCode = entity.Code }
            });

            return entity;
        }
        private TypeDef CreateMetadataType()
        {
            int ordinal = 0;

            TypeDef metadata = new()
            {
                Ref = new Entity(2, Guid.NewGuid()),
                Code = 2,
                Name = "Metadata",
                BaseType = _entity.Ref
            };

            metadata.Properties.Add(new PropertyDef()
            {
                Ref = new Entity(4, Guid.NewGuid()),
                Name = "Name",
                Owner = metadata.Ref,
                Ordinal = ++ordinal,
                ColumnName = "name",
                Qualifier1 = 64,
                DataType = new UnionType() { IsString = true }
            });

            metadata.Properties.Add(new PropertyDef()
            {
                Ref = new Entity(4, Guid.NewGuid()),
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
            int ordinal = 0;

            TypeDef definition = new()
            {
                Ref = new Entity(3, Guid.NewGuid()),
                Code = 3,
                Name = "TypeDef",
                TableName = "md_types",
                BaseType = _metadata.Ref
            };

            // Ref property derived from ENTITY

            definition.Properties.Add(new PropertyDef()
            {
                Ref = new Entity(4, Guid.NewGuid()),
                Code = 4,
                Name = "Ref",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "_ref",
                IsPrimaryKey = true,
                Qualifier1 = 16,
                DataType = new UnionType() { IsEntity = true, TypeCode = definition.Code }
            });

            // Properties derived from Metadata class

            definition.Properties.Add(new PropertyDef()
            {
                Ref = new Entity(4, Guid.NewGuid()),
                Name = "Name",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "name",
                Qualifier1 = 64,
                DataType = new UnionType() { IsString = true }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Ref = new Entity(4, Guid.NewGuid()),
                Name = "Code",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "type_code",
                IsIdentity = true,
                DataType = new UnionType() { IsInteger = true }
            });

            // TypeDef class own properties

            definition.Properties.Add(new PropertyDef()
            {
                Ref = new Entity(4, Guid.NewGuid()),
                Name = "TableName",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "table_name",
                Qualifier1 = 32,
                DataType = new UnionType() { IsString = true }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Ref = new Entity(4, Guid.NewGuid()),
                Name = "BaseType",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "base_type",
                Qualifier1 = 16,
                DataType = new UnionType() { IsEntity = true, TypeCode = definition.Code }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Ref = new Entity(4, Guid.NewGuid()),
                Name = "NestType",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "nest_type",
                Qualifier1 = 16,
                DataType = new UnionType() { IsEntity = true, TypeCode = definition.Code }
            });

            return definition;
        }
        public void CreateSystemDatabase()
        {
            List<string> sql = new()
            {
                BuildCreateTableScript(in _definition),
                BuildCreateIndexScript(in _definition)
            };

            _executor.TxExecuteNonQuery(in sql, 10);

            CreateTypeDef(in _entity); // ENTITY
            CreateTypeDef(in _metadata); // Metadata
            CreateTypeDef(in _definition); // TypeDef
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
                .Append("INSERT md_types (_ref, name, table_name, base_type, nest_type)")
                .AppendLine()
                .Append("OUTPUT INSERTED.type_code AS type_code")
                .AppendLine()
                .Append("SELECT @_ref, @name, @table_name, @base_type, @nest_type")
                .ToString();

            Dictionary<string, object> parameters = new()
            {
                { "_ref", definition.Ref.Identity.ToByteArray() },
                { "name", definition.Name is null ? string.Empty : definition.Name },
                { "table_name", definition.TableName is null ? string.Empty : definition.TableName },
                { "base_type", definition.BaseType.IsUndefined ? Guid.Empty.ToByteArray() : definition.BaseType.Identity.ToByteArray() },
                { "nest_type", definition.NestType.IsUndefined ? Guid.Empty.ToByteArray() : definition.NestType.Identity.ToByteArray() }
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
                .Append("SELECT _ref, name, type_code, table_name, base_type, nest_type")
                .AppendLine()
                .Append("FROM md_types")
                .AppendLine()
                .Append("WHERE name = @name")
                .ToString();

            Dictionary<string, object> parameters = new()
            {
                { "name", identifier }
            };

            foreach (IDataReader reader in _executor.ExecuteReader(script, 10, parameters))
            {
                definition.Ref = new Entity(3, new Guid((byte[])reader["_ref"]));
                definition.Code = (int)reader["type_code"];
                definition.Name = (string)reader["name"];
                definition.TableName = (string)reader["table_name"];
                definition.BaseType = new Entity(3, new Guid((byte[])reader["base_type"]));
                definition.NestType = new Entity(3, new Guid((byte[])reader["nest_type"]));
            }

            definition.BaseType = _metadata.Ref;

            SelectProperties(in definition);

            return definition;
        }
        private void SelectProperties(in TypeDef definition)
        {
            int ordinal = 0;

            // Ref property derived from ENTITY

            definition.Properties.Add(new PropertyDef()
            {
                Ref = new Entity(4, Guid.NewGuid()),
                Code = 4,
                Name = "Ref",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "_ref",
                IsPrimaryKey = true,
                Qualifier1 = 16,
                DataType = new UnionType() { IsEntity = true, TypeCode = definition.Code }
            });

            // Properties derived from Metadata class

            definition.Properties.Add(new PropertyDef()
            {
                Ref = new Entity(4, Guid.NewGuid()),
                Name = "Name",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "name",
                Qualifier1 = 64,
                DataType = new UnionType() { IsString = true }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Ref = new Entity(4, Guid.NewGuid()),
                Name = "Code",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "type_code",
                IsIdentity = true,
                DataType = new UnionType() { IsInteger = true }
            });

            // TypeDef class own properties

            definition.Properties.Add(new PropertyDef()
            {
                Ref = new Entity(4, Guid.NewGuid()),
                Name = "TableName",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "table_name",
                Qualifier1 = 32,
                DataType = new UnionType() { IsString = true }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Ref = new Entity(4, Guid.NewGuid()),
                Name = "BaseType",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "base_type",
                Qualifier1 = 16,
                DataType = new UnionType() { IsEntity = true, TypeCode = definition.Code }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Ref = new Entity(4, Guid.NewGuid()),
                Name = "NestType",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "nest_type",
                Qualifier1 = 16,
                DataType = new UnionType() { IsEntity = true, TypeCode = definition.Code }
            });
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