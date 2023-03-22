using DaJet.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Text;

namespace DaJet.Model
{
    public sealed class MsDbConfigurator : IDbConfigurator
    {
        private readonly TypeDef ENTITY;
        private readonly TypeDef METADATA;
        private readonly TypeDef TYPE_DEF;
        private readonly TypeDef PROPERTY_DEF;
        private readonly TypeDef RELATION_DEF;

        private readonly Dictionary<int, TypeDef> TYPES;

        private readonly IQueryExecutor _executor;
        public MsDbConfigurator(IQueryExecutor executor)
        {
            _executor = executor;

            ENTITY = CreateEntity();
            METADATA = CreateMetadataType();
            TYPE_DEF = CreateTypeDefinition();
            PROPERTY_DEF = CreatePropertyDefinition();
            RELATION_DEF = CreateRelationDefinition();

            TYPES = new Dictionary<int, TypeDef>()
            {
                { SystemTypeCode.Entity, ENTITY },
                { SystemTypeCode.Metadata, METADATA },
                { SystemTypeCode.TypeDef, TYPE_DEF },
                { SystemTypeCode.PropertyDef, PROPERTY_DEF },
                { SystemTypeCode.RelationDef, RELATION_DEF }
            };
        }

        #region "CREATE AND CONFIGURE SYSTEM DATABASE"
        public void CreateDatabase()
        {
            List<string> sql;

            if (!TableExists(TYPE_DEF.TableName))
            {
                sql = new()
                {
                    BuildCreateTableScript(in TYPE_DEF),
                    BuildCreateIndexScript(in TYPE_DEF)
                };
                _executor.TxExecuteNonQuery(in sql, 10);

                CreateTypeDef(in ENTITY);
                CreateTypeDef(in METADATA);
                CreateTypeDef(in TYPE_DEF);
                CreateTypeDef(in PROPERTY_DEF);
                CreateTypeDef(in RELATION_DEF);
            }

            if (!TableExists(PROPERTY_DEF.TableName))
            {
                sql = new()
                {
                    BuildCreateTableScript(in PROPERTY_DEF),
                    BuildCreateIndexScript(in PROPERTY_DEF)
                };
                _executor.TxExecuteNonQuery(in sql, 10);

                CreatePropertyDef(in ENTITY);
                CreatePropertyDef(in METADATA);
                CreatePropertyDef(in TYPE_DEF);
                CreatePropertyDef(in PROPERTY_DEF);
                CreatePropertyDef(in RELATION_DEF);
            }

            if (!TableExists(RELATION_DEF.TableName))
            {
                sql = new()
                {
                    BuildCreateTableScript(in RELATION_DEF),
                    BuildCreateIndexScript(in RELATION_DEF)
                };
                _executor.TxExecuteNonQuery(in sql, 10);

                CreateRelationDef(in ENTITY);
                CreateRelationDef(in METADATA);
                CreateRelationDef(in TYPE_DEF);
                CreateRelationDef(in PROPERTY_DEF);
                CreateRelationDef(in RELATION_DEF);
            }
        }
        private TypeDef CreateEntity()
        {
            TypeDef entity = new()
            {
                Code = SystemTypeCode.Entity, Name = "ENTITY",
                Ref = new Entity(SystemTypeCode.TypeDef, SystemTypeUuid.Entity)
            };

            entity.Properties.Add(new PropertyDef()
            {
                Name = "Ref",
                Owner = entity.Ref,
                Ordinal = 1,
                ColumnName = "entity_ref",
                IsPrimaryKey = true,
                DataType = new UnionType() { IsEntity = true, TypeCode = SystemTypeCode.Entity }
                //IMPORTANT: TypeCode must be overridden by a derived class !!!
            });

            return entity;
        }
        private TypeDef CreateMetadataType()
        {
            TypeDef metadata = new()
            {
                Code = SystemTypeCode.Metadata, Name = "Metadata", BaseType = ENTITY.Ref,
                Ref = new Entity(SystemTypeCode.TypeDef, SystemTypeUuid.Metadata)
            };

            int ordinal = 0;

            metadata.Properties.Add(new PropertyDef()
            {
                Name = "Name",
                Owner = metadata.Ref,
                Ordinal = ++ordinal,
                ColumnName = "meta_name",
                Qualifier1 = 64,
                DataType = new UnionType() { IsString = true }
            });

            metadata.Properties.Add(new PropertyDef()
            {
                Name = "Code",
                Owner = metadata.Ref,
                Ordinal = ++ordinal,
                ColumnName = "meta_code",
                DataType = new UnionType() { IsInteger = true }
            });

            return metadata;
        }
        private TypeDef CreateTypeDefinition()
        {
            TypeDef definition = new()
            {
                Code = SystemTypeCode.TypeDef,
                Name = "TypeDef",
                BaseType = METADATA.Ref,
                TableName = "dajet_types",
                IsTemplate = false,
                Ref = new Entity(SystemTypeCode.TypeDef, SystemTypeUuid.TypeDef)
            };

            int ordinal = 0;

            // Properties derived from ENTITY

            foreach (PropertyDef property in ENTITY.Properties)
            {
                PropertyDef new_prop = property.Copy();
                new_prop.Owner = definition.Ref;
                new_prop.Ordinal = ++ordinal;
                if (property.IsPrimaryKey) // Ref - override TypeCode = self reference
                {
                    new_prop.DataType = new UnionType() { IsEntity = true, TypeCode = SystemTypeCode.TypeDef };
                }
                definition.Properties.Add(new_prop);
            }

            // Properties derived from METADATA

            foreach (PropertyDef property in METADATA.Properties)
            {
                PropertyDef new_prop = property.Copy();
                new_prop.Owner = definition.Ref;
                new_prop.Ordinal = ++ordinal;
                definition.Properties.Add(new_prop);
            }

            // TypeDef class own properties

            definition.Properties.Add(new PropertyDef()
            {
                Name = "IsTemplate",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "is_template",
                DataType = new UnionType() { IsBoolean = true }
            });

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
                DataType = new UnionType() { IsEntity = true, TypeCode = SystemTypeCode.TypeDef }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Name = "NestType",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "nest_type",
                DataType = new UnionType() { IsEntity = true, TypeCode = SystemTypeCode.TypeDef }
            });

            return definition;
        }
        private TypeDef CreatePropertyDefinition()
        {
            TypeDef definition = new()
            {
                Code = SystemTypeCode.PropertyDef,
                Name = "PropertyDef",
                BaseType = METADATA.Ref,
                TableName = "dajet_properties",
                IsTemplate = false,
                Ref = new Entity(SystemTypeCode.TypeDef, SystemTypeUuid.PropertyDef)
            };

            int ordinal = 0;

            // Properties derived from ENTITY

            foreach (PropertyDef property in ENTITY.Properties)
            {
                PropertyDef new_prop = property.Copy();
                new_prop.Owner = definition.Ref;
                new_prop.Ordinal = ++ordinal;
                if (property.IsPrimaryKey) // Ref - override TypeCode = self reference
                {
                    new_prop.DataType = new UnionType() { IsEntity = true, TypeCode = SystemTypeCode.PropertyDef };
                }
                definition.Properties.Add(new_prop);
            }

            // Properties derived from METADATA

            foreach (PropertyDef property in METADATA.Properties)
            {
                PropertyDef new_prop = property.Copy();
                new_prop.Owner = definition.Ref;
                new_prop.Ordinal = ++ordinal;
                if (property.Name == "Code") // override Code property
                {
                    new_prop.IsIdentity = true;
                    new_prop.IdentitySeed = 1;
                    new_prop.IdentityIncrement = 1;
                }
                definition.Properties.Add(new_prop);
            }

            #region "PropertyDef class own properties"

            definition.Properties.Add(new PropertyDef()
            {
                Name = "Owner",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "owner_ref",
                DataType = new UnionType() { IsEntity = true, TypeCode = SystemTypeCode.TypeDef }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Name = "Ordinal",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "ordinal",
                DataType = new UnionType() { IsInteger = true }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Name = "DataType",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "data_type",
                DataType = new UnionType() { IsInteger = true }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Name = "Qualifier1",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "qualifier1",
                DataType = new UnionType() { IsInteger = true }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Name = "Qualifier2",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "qualifier2",
                DataType = new UnionType() { IsInteger = true }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Name = "ColumnName",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "column_name",
                Qualifier1 = 64,
                DataType = new UnionType() { IsString = true }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Name = "IsNullable",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "is_nullable",
                DataType = new UnionType() { IsBoolean = true }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Name = "IsPrimaryKey",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "is_primary_key",
                DataType = new UnionType() { IsBoolean = true }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Name = "IsVersion",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "is_version",
                DataType = new UnionType() { IsBoolean = true }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Name = "IsIdentity",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "is_identity",
                DataType = new UnionType() { IsBoolean = true }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Name = "IdentitySeed",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "identity_seed",
                DataType = new UnionType() { IsInteger = true }
            });

            definition.Properties.Add(new PropertyDef()
            {
                Name = "IdentityIncrement",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "identity_increment",
                DataType = new UnionType() { IsInteger = true }
            });

            #endregion

            return definition;
        }
        private TypeDef CreateRelationDefinition()
        {
            TypeDef definition = new()
            {
                Code = SystemTypeCode.RelationDef,
                Name = "RelationDef",
                TableName = "dajet_relations",
                IsTemplate = false,
                Ref = new Entity(SystemTypeCode.TypeDef, SystemTypeUuid.RelationDef)
            };

            int ordinal = 0;

            // RelationDef class own properties

            definition.Properties.Add(new PropertyDef()
            {
                Name = "Source",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "source",
                IsPrimaryKey = true,
                DataType = new UnionType() { IsEntity = true, TypeCode = SystemTypeCode.PropertyDef } // TODO: PropertyDef | UnionDef
            });

            definition.Properties.Add(new PropertyDef()
            {
                Name = "Target",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "target",
                IsPrimaryKey = true,
                DataType = new UnionType() { IsEntity = true, TypeCode = SystemTypeCode.TypeDef }
            });

            return definition;
        }

        private bool TableExists(in string tableName)
        {
            string script = SQLHelper.GetTableExistsScript(in tableName);
            return (_executor.ExecuteScalar<int>(in script, 10) == 1);
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

                UnionTag tag;
                if (definition == ENTITY && property.IsPrimaryKey) // Ref
                {
                    tag = UnionTag.Entity;
                }
                else if (property.DataType.IsUnion)
                {
                    //TODO: create multiple columns with postfixes
                    tag = property.DataType.GetSingleTagOrUndefined();
                }
                else
                {
                    tag = property.DataType.GetSingleTagOrUndefined();
                }

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

                //TODO: union type columns

                script.Append(property.ColumnName).Append(" ASC");
            }

            script.Append(");");

            return script.ToString();
        }

        public static string GetDbTypeName(UnionTag tag)
        {
            if (tag == UnionTag.Tag) { return "binary(1)"; }
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
        private void FormatQueryParameters(in Dictionary<string, object> parameters)
        {
            foreach (var parameter in parameters)
            {
                if (parameter.Value is Guid uuid)
                {
                    parameters[parameter.Key] = uuid.ToByteArray();
                }
                else if (parameter.Value is bool boolean)
                {
                    parameters[parameter.Key] = new byte[] { Convert.ToByte(boolean) };
                }
                else if (parameter.Value is Entity entity)
                {
                    parameters[parameter.Key] = entity.Identity.ToByteArray();
                }
                else if (parameter.Value is Union union)
                {
                    if (union.Tag == UnionTag.Uuid)
                    {
                        parameters[parameter.Key] = union.GetUuid().ToByteArray();
                    }
                    else if (union.Tag == UnionTag.Entity)
                    {
                        parameters[parameter.Key] = union.GetEntity().Identity.ToByteArray();
                    }
                    else if (union.Tag == UnionTag.Boolean)
                    {
                        parameters[parameter.Key] = new byte[] { Convert.ToByte(union.GetBoolean()) };
                    }
                    else
                    {
                        parameters[parameter.Key] = union.Value;
                    }
                }
                else if (parameter.Value is UnionType type)
                {
                    parameters[parameter.Key] = type.Flags;
                }
            }
        }

        private void CreateTypeDef(in TypeDef definition)
        {
            string script = new StringBuilder()
                .Append("INSERT dajet_types(entity_ref, is_template, meta_name, meta_code, table_name, base_type, nest_type)")
                .AppendLine()
                .Append("SELECT @entity_ref, @is_template, @meta_name, @meta_code, @table_name, @base_type, @nest_type")
                .ToString();

            Dictionary<string, object> parameters = new()
            {
                { "entity_ref", definition.Ref },
                { "is_template", definition.IsTemplate },
                { "meta_name", definition.Name is null ? string.Empty : definition.Name },
                { "meta_code", definition.Code },
                { "table_name", definition.TableName is null ? string.Empty : definition.TableName },
                { "base_type", definition.BaseType },
                { "nest_type", definition.NestType }
            };

            FormatQueryParameters(in parameters);

            foreach (IDataReader reader in _executor.ExecuteReader(script, 10, parameters))
            {
                //do nothing
            }
        }
        public TypeDef SelectTypeDef(Entity entity)
        {
            TypeDef definition = null;

            string script = new StringBuilder()
                .Append("SELECT entity_ref, is_template, meta_name, meta_code, table_name, base_type, nest_type")
                .AppendLine()
                .Append("FROM ").Append(TYPE_DEF.TableName)
                .AppendLine()
                .Append("WHERE entity_ref = @entity_ref")
                .ToString();

            Dictionary<string, object> parameters = new() { { "entity_ref", entity.Identity.ToByteArray() } };

            foreach (IDataReader reader in _executor.ExecuteReader(script, 10, parameters))
            {
                definition = new()
                {
                    Ref = new Entity(SystemTypeCode.TypeDef, new Guid((byte[])reader["entity_ref"])),
                    Code = (int)reader["meta_code"],
                    Name = (string)reader["meta_name"],
                    TableName = (string)reader["table_name"],
                    IsTemplate = (((byte[])reader["is_template"])[0] == 1),
                    BaseType = new Entity(SystemTypeCode.TypeDef, new Guid((byte[])reader["base_type"])),
                    NestType = new Entity(SystemTypeCode.TypeDef, new Guid((byte[])reader["nest_type"]))
                };
            }

            if (definition is not null) { definition.Properties.AddRange(SelectPropertyDef(in definition)); }

            return definition;
        }
        public TypeDef SelectTypeDef(in string identifier)
        {
            int count = 0;
            TypeDef definition = new();
            Dictionary<string, object> parameters = new();

            string script = BuildSelectTypeDefScript(in identifier, in parameters);

            foreach (IDataReader reader in _executor.ExecuteReader(script, 10, parameters))
            {
                count++;
                definition.Ref = new Entity(SystemTypeCode.TypeDef, new Guid((byte[])reader["entity_ref"]));
                definition.Code = (int)reader["meta_code"];
                definition.Name = (string)reader["meta_name"];
                definition.TableName = (string)reader["table_name"];
                definition.IsTemplate = (((byte[])reader["is_template"])[0] == 1);
                definition.BaseType = new Entity(SystemTypeCode.TypeDef, new Guid((byte[])reader["base_type"]));
                definition.NestType = new Entity(SystemTypeCode.TypeDef, new Guid((byte[])reader["nest_type"]));
            }

            if (count == 0) { return null; } // TypeDef is not found by identifier
            
            if (count > 1) { throw new InvalidOperationException($"Ambiguous name: [{identifier}]"); }

            if (definition.Ref == ENTITY.Ref)
            {
                definition.Properties.AddRange(ENTITY.Properties);
            }
            else if (definition.Ref == TYPE_DEF.Ref)
            {
                definition.Properties.AddRange(TYPE_DEF.Properties);
            }
            else if (definition.Ref == PROPERTY_DEF.Ref)
            {
                definition.Properties.AddRange(PROPERTY_DEF.Properties);
            }
            else if (definition.Ref == RELATION_DEF.Ref)
            {
                definition.Properties.AddRange(RELATION_DEF.Properties);
            }
            else
            {
                definition.Properties.AddRange(SelectPropertyDef(in definition));
            }

            return definition;
        }
        private string BuildSelectTypeDefScript(in string identifier, in Dictionary<string, object> parameters)
        {
            parameters.Clear();
            List<string> tables = new();

            string[] names = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            for (int i = 0; i < names.Length; i++)
            {
                tables.Add($"t{i}");
                parameters.Add($"name{i}", names[i]);
            }

            int countdown = names.Length;

            StringBuilder script = new();

            (--countdown).ToString();
            script.Append("SELECT ");
            script.Append(tables[countdown]).Append(".entity_ref, ");
            script.Append(tables[countdown]).Append(".is_template, ");
            script.Append(tables[countdown]).Append(".meta_name, ");
            script.Append(tables[countdown]).Append(".meta_code, ");
            script.Append(tables[countdown]).Append(".table_name, ");
            script.Append(tables[countdown]).Append(".base_type, ");
            script.Append(tables[countdown]).Append(".nest_type");
            script.AppendLine();
            script.Append("FROM (SELECT entity_ref, is_template, meta_name, meta_code, table_name, base_type, nest_type");
            script.Append(" FROM ").Append(TYPE_DEF.TableName).Append(" WHERE meta_name = @name").Append(countdown);
            script.Append(") AS ").Append(tables[countdown]);

            while (countdown > 0)
            {
                (--countdown).ToString();
                script.AppendLine();
                script.Append("INNER JOIN ").Append(TYPE_DEF.TableName).Append(" AS ").Append(tables[countdown]);
                script.Append(" ON ").Append(tables[countdown]).Append(".meta_name = @name").Append(countdown);
                script.Append(" AND (");
                script.Append(tables[countdown + 1]).Append(".base_type = ").Append(tables[countdown]).Append(".entity_ref");
                script.Append(" OR ");
                script.Append(tables[countdown + 1]).Append(".nest_type = ").Append(tables[countdown]).Append(".entity_ref");
                script.Append(')');
            }

            return script.ToString();
        }

        private void CreatePropertyDef(in TypeDef owner)
        {
            Type type = typeof(PropertyDef);
            string script = BuildCreatePropertyScript();
            Dictionary<string, string> column_map = new();
            Dictionary<string, object> parameters = new();

            foreach (PropertyDef property in PROPERTY_DEF.Properties)
            {
                if (property.IsVersion || property.IsIdentity)
                {
                    continue;
                }
                column_map.Add(property.Name, property.ColumnName);
            }

            foreach (PropertyDef property in owner.Properties)
            {
                string columnName;
                parameters.Clear();
                foreach (PropertyInfo info in type.GetProperties())
                {
                    if (column_map.TryGetValue(info.Name, out columnName))
                    {
                        parameters.Add(columnName, info.GetValue(property));
                    }
                }
                FormatQueryParameters(in parameters);

                foreach (IDataReader reader in _executor.ExecuteReader(script, 10, parameters))
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        if (reader.GetName(i) == "meta_code")
                        {
                            property.Code = reader.GetInt32(i); break;
                        }
                    }
                }
            }
        }
        private string BuildCreatePropertyScript()
        {
            StringBuilder script = new();
            StringBuilder insert = new();
            StringBuilder select = new();

            insert.Append("INSERT ").Append(PROPERTY_DEF.TableName).Append('(');
            select.Append("SELECT ");

            PropertyDef property;
            List<PropertyDef> properties = PROPERTY_DEF.Properties;

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
        private List<PropertyDef> SelectPropertyDef(in TypeDef owner)
        {
            List<PropertyDef> properties = new();

            string script = new StringBuilder()
                .Append("SELECT entity_ref, meta_name, meta_code, owner_ref, ordinal, data_type, qualifier1, qualifier2, ")
                .Append("column_name, is_nullable, is_primary_key, is_version, is_identity, identity_seed, identity_increment")
                .AppendLine()
                .Append("FROM ").Append(PROPERTY_DEF.TableName)
                .AppendLine()
                .Append("WHERE owner_ref = @owner_ref")
                .ToString();

            Dictionary<string, object> parameters = new() { { "owner_ref", owner.Ref.Identity.ToByteArray() } };

            foreach (IDataReader reader in _executor.ExecuteReader(script, 10, parameters))
            {
                PropertyDef property = new()
                {
                    Ref = new Entity(SystemTypeCode.PropertyDef, new Guid((byte[])reader["entity_ref"])),
                    Code = (int)reader["meta_code"],
                    Name = (string)reader["meta_name"],
                    Owner = new Entity(owner.Code, new Guid((byte[])reader["owner_ref"])),
                    Ordinal = (int)reader["ordinal"],
                    DataType = new UnionType() { Flags = (int)reader["data_type"], TypeCode = 0 },
                    Qualifier1 = (int)reader["qualifier1"],
                    Qualifier2 = (int)reader["qualifier2"],
                    ColumnName = (string)reader["column_name"],
                    IsNullable = (((byte[])reader["is_nullable"])[0] == 1),
                    IsPrimaryKey = (((byte[])reader["is_primary_key"])[0] == 1),
                    IsVersion = (((byte[])reader["is_version"])[0] == 1),
                    IsIdentity = (((byte[])reader["is_identity"])[0] == 1),
                    IdentitySeed = (int)reader["identity_seed"],
                    IdentityIncrement = (int)reader["identity_increment"]
                };

                property.Relations.AddRange(SelectRelationDef(in property));

                properties.Add(property);
            }

            return properties;
        }

        private void CreateRelationDef(in TypeDef definition)
        {
            string script = new StringBuilder()
                .Append("INSERT dajet_relations(source, target)")
                .AppendLine()
                .Append("SELECT @source, @target")
                .ToString();

            TypeDef target;
            Dictionary<string, object> parameters = new();

            foreach (PropertyDef property in definition.Properties)
            {
                //if (property.Name == "Ref") { continue; } // self reference

                if (!property.DataType.IsEntity) { continue; }

                if (definition == ENTITY && property.IsPrimaryKey) // Ref: TypeCode == 0
                {
                    //do nothing
                }
                else if (property.DataType.TypeCode == 0) { continue; } //TODO: multiple references

                if (!TYPES.TryGetValue(property.DataType.TypeCode, out target))
                {
                    throw new InvalidOperationException($"TypeDef code [{property.DataType.TypeCode}] not found.");
                }

                parameters.Clear();
                parameters.Add("source", property.Ref);
                parameters.Add("target", target.Ref);
                FormatQueryParameters(in parameters);

                foreach (IDataReader reader in _executor.ExecuteReader(script, 10, parameters))
                {
                    // do nothing
                }
            }
        }
        private List<RelationDef> SelectRelationDef(in PropertyDef property)
        {
            List<RelationDef> relations = new();

            string script = new StringBuilder()
                .Append("SELECT source, target")
                .AppendLine()
                .Append("FROM ").Append(RELATION_DEF.TableName)
                .AppendLine()
                .Append("WHERE source = @source")
                .ToString();

            Dictionary<string, object> parameters = new() { { "source", property.Ref.Identity.ToByteArray() } };

            foreach (IDataReader reader in _executor.ExecuteReader(script, 10, parameters))
            {
                RelationDef relation = new()
                {
                    Source = new Entity(SystemTypeCode.PropertyDef, new Guid((byte[])reader["source"])),
                    Target = new Entity(SystemTypeCode.TypeDef, new Guid((byte[])reader["target"]))
                };

                relations.Add(relation);
            }

            return relations;
        }
        #endregion

        public int GenerateTypeCode()
        {
            string script = new StringBuilder()
                .Append("SELECT MAX(meta_code) + 1 FROM ")
                .Append(TYPE_DEF.TableName)
                .Append(" WITH (TABLOCKX)")
                .ToString();

            return _executor.ExecuteScalar<int>(in script, 10);
        }
        public void CreateUserType(in TypeDef definition)
        {
            string identifier = GetFullName(in definition);

            if (SelectTypeDef(in identifier) is null)
            {
                CreateTypeDef(in definition);
            }
        }
        public void CreateProperties(in TypeDef definition)
        {
            CreatePropertyDef(in definition);
        }
        public void CreateRelations(in TypeDef definition)
        {
            CreateRelationDef(in definition);
        }
        public string GetFullName(in TypeDef definition)
        {
            StringBuilder name = new();
            
            GetFullName(in definition, in name);

            name.Remove(0, 1); // leading dot

            return name.ToString();
        }
        private void GetFullName(in TypeDef definition, in StringBuilder name)
        {
            if (definition.BaseType == Entity.Undefined) { return; }

            TypeDef parent = SelectTypeDef(definition.BaseType);

            if (parent is not null) { GetFullName(in parent, in name); }

            name.Append('.').Append(definition.Name);
        }
    }
}