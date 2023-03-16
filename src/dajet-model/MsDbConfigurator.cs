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
        }

        private TypeDef GetMetadataType()
        {
            int ordinal = TypeDef.Entity.Properties.Count;

            TypeDef metadata = new()
            {
                Ref = Guid.NewGuid(),
                Code = 2,
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
                IsIdentity = true,
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
                Qualifier1 = 16,
                DataType = new UnionType() { IsEntity = true, TypeCode = definition.Code }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Ref = Guid.NewGuid(),
                Name = "NestType",
                Owner = definition,
                Ordinal = ++ordinal,
                ColumnName = "nest_type",
                Qualifier1 = 16,
                DataType = new UnionType() { IsEntity = true, TypeCode = definition.Code }
            });

            return definition;
        }
        public void CreateSystemDatabase()
        {
            TypeDef definition = GetTypeDef();

            List<string> sql = new()
            {
                BuildCreateTableScript(in definition),
                BuildCreateIndexScript(in definition)
            };

            _executor.TxExecuteNonQuery(in sql, 10);

            CreateTypeDef(TypeDef.Entity); // ENTITY

            CreateTypeDef(definition.BaseType); // Metadata
            
            CreateTypeDef(in definition); // TypeDef
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
                { "_ref", definition.Ref.ToByteArray() },
                { "name", definition.Name is null ? string.Empty : definition.Name },
                { "table_name", definition.TableName is null ? string.Empty : definition.TableName },
                { "base_type", definition.BaseType is null ? Guid.Empty.ToByteArray() : definition.BaseType.Ref.ToByteArray() },
                { "nest_type", definition.NestType is null ? Guid.Empty.ToByteArray() : definition.NestType.Ref.ToByteArray() }
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
                definition.Ref = new Guid((byte[])reader["_ref"]);
                definition.Code = (int)reader["type_code"];
                definition.Name = (string)reader["name"];
                definition.TableName = (string)reader["table_name"];
                definition.BaseType = new TypeDef() { Ref = new Guid((byte[])reader["base_type"]), Code = 3 };
                definition.NestType = new TypeDef() { Ref = new Guid((byte[])reader["nest_type"]), Code = 3 };
            }

            definition.BaseType = GetMetadataType();

            SelectProperties(in definition);

            return definition;
        }
        private void SelectProperties(in TypeDef definition)
        {
            int ordinal = definition.BaseType.Properties.Count + definition.BaseType.BaseType.Properties.Count;

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
                Qualifier1 = 16,
                DataType = new UnionType() { IsEntity = true, TypeCode = definition.Code }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Ref = Guid.NewGuid(),
                Name = "NestType",
                Owner = definition,
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
            List<PropertyDef> properties = definition.GetProperties();

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
            List<PropertyDef> properties = definition.GetProperties();

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