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

        private readonly IQueryExecutor _executor;
        public MsDbConfigurator(IQueryExecutor executor)
        {
            _executor = executor;

            ENTITY = CreateEntity();
            METADATA = CreateMetadataType();
            TYPE_DEF = CreateTypeDefinition();
            PROPERTY_DEF = CreatePropertyDefinition();
            RELATION_DEF = CreateRelationDefinition();
        }

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

                //TODO: RelationDef for properties
            }
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
                ColumnName = "entity_ref",
                IsPrimaryKey = true,
                DataType = new UnionType() { IsEntity = true, TypeCode = entity.Ref.TypeCode }
            });

            return entity;
        }
        private TypeDef CreateMetadataType()
        {
            TypeDef metadata = new()
            {
                Name = "METADATA", BaseType = ENTITY.Ref, Ref = new Entity(1, new Guid("43ED3777-C00B-4E45-9D8B-5783771C184B"))
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

            int ordinal = 0;

            // Properties derived from ENTITY

            foreach (PropertyDef property in ENTITY.Properties)
            {
                PropertyDef new_prop = property.Copy();
                new_prop.Owner = definition.Ref;
                new_prop.Ordinal = ++ordinal;
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
            TypeDef definition = new()
            {
                Name = "PropertyDef",
                BaseType = METADATA.Ref,
                TableName = "dajet_properties",
                Ref = new Entity(1, new Guid("DF207406-A318-4AD8-858A-2250D0B485B8"))
            };

            int ordinal = 0;

            // Properties derived from ENTITY

            foreach (PropertyDef property in ENTITY.Properties)
            {
                PropertyDef new_prop = property.Copy();
                new_prop.Owner = definition.Ref;
                new_prop.Ordinal = ++ordinal;
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

            // PropertyDef class own properties

            definition.Properties.Add(new PropertyDef()
            {
                Name = "Owner",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "owner_ref",
                DataType = new UnionType() { IsEntity = true, TypeCode = TYPE_DEF.Ref.TypeCode }
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

            return definition;
        }
        private TypeDef CreateRelationDefinition()
        {
            TypeDef definition = new()
            {
                Name = "RelationDef",
                TableName = "dajet_relations",
                Ref = new Entity(1, new Guid("4945B787-20CB-4CDF-AAE8-6E00A19A4CD8"))
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
                DataType = new UnionType() { IsEntity = true, TypeCode = PROPERTY_DEF.Ref.TypeCode } // TODO: PropertyDef | UnionDef
            });

            definition.Properties.Add(new PropertyDef()
            {
                Name = "Target",
                Owner = definition.Ref,
                Ordinal = ++ordinal,
                ColumnName = "target",
                IsPrimaryKey = true,
                DataType = new UnionType() { IsEntity = true, TypeCode = TYPE_DEF.Ref.TypeCode }
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

                UnionTag tag = property.DataType.GetSingleTagOrUndefined();

                if (property.DataType.IsUnion)
                {
                    //TODO: create multiple columns with postfixes
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
                .Append("INSERT dajet_types(entity_ref, meta_name, table_name, base_type, nest_type)")
                .AppendLine()
                .Append("OUTPUT INSERTED.meta_code AS meta_code")
                .AppendLine()
                .Append("SELECT @entity_ref, @meta_name, @table_name, @base_type, @nest_type")
                .ToString();

            Dictionary<string, object> parameters = new()
            {
                { "entity_ref", definition.Ref.Identity.ToByteArray() },
                { "meta_name", definition.Name is null ? string.Empty : definition.Name },
                { "table_name", definition.TableName is null ? string.Empty : definition.TableName },
                { "base_type", definition.BaseType.Identity.ToByteArray() },
                { "nest_type", definition.NestType.Identity.ToByteArray() }
            };

            foreach (IDataReader reader in _executor.ExecuteReader(script, 10, parameters))
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.GetName(i) == "meta_code")
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
                .Append("SELECT entity_ref, meta_name, meta_code, table_name, base_type, nest_type")
                .AppendLine()
                .Append("FROM dajet_types")
                .AppendLine()
                .Append("WHERE meta_name = @name")
                .ToString();

            Dictionary<string, object> parameters = new()
            {
                { "name", identifier }
            };

            foreach (IDataReader reader in _executor.ExecuteReader(script, 10, parameters))
            {
                definition.Ref = new Entity(definition.Ref.TypeCode, new Guid((byte[])reader["entity_ref"]));
                definition.Code = (int)reader["meta_code"];
                definition.Name = (string)reader["meta_name"];
                definition.TableName = (string)reader["table_name"];
                definition.BaseType = new Entity(definition.Ref.TypeCode, new Guid((byte[])reader["base_type"]));
                definition.NestType = new Entity(definition.Ref.TypeCode, new Guid((byte[])reader["nest_type"]));
            }

            if (definition.Ref == TYPE_DEF.Ref)
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

            return definition;
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