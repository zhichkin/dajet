using DaJet.Data;
using DaJet.Metadata.Model;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;

namespace DaJet.Sqlite
{
    public sealed class SqliteDbConfigurator : IDbConfigurator
    {
        private readonly string _connectionString;
        public SqliteDbConfigurator(in string databaseFileFullPath)
        {
            if (string.IsNullOrWhiteSpace(databaseFileFullPath))
            {
                throw new ArgumentNullException(nameof(databaseFileFullPath));
            }

            _connectionString = new SqliteConnectionStringBuilder()
            {
                DataSource = databaseFileFullPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }
            .ToString();
        }
        public bool TryCreateType(in UserDefinedType type) { throw new NotImplementedException(); }
        public UserDefinedType GetTypeDefinition(in string identifier) { throw new NotImplementedException(); }

        #region "CONFIGURE DATABASE"

        private const string CREATE_SEQUENCE_TABLE =
            "CREATE TABLE IF NOT EXISTS dajet_sequence(value INTEGER NOT NULL); " +
            "INSERT INTO dajet_sequence(rowid, value) VALUES(1, 0) ON CONFLICT (rowid) DO NOTHING;";
        private const string GET_NEXT_SEQUENCE_VALUE =
            "UPDATE dajet_sequence SET value = value + 1 WHERE rowid = 1 RETURNING value;";
        private const string CREATE_NAMESPACE_TABLE =
            "CREATE TABLE IF NOT EXISTS dajet_namespace " +
            "(uuid TEXT NOT NULL, name TEXT NOT NULL, parent TEXT, " +
            "PRIMARY KEY (uuid)) WITHOUT ROWID;";
        private const string CREATE_ENTITY_TABLE =
            "CREATE TABLE IF NOT EXISTS dajet_entity " +
            "(uuid TEXT NOT NULL, type INTEGER NOT NULL, " +
            "code INTEGER NOT NULL, name TEXT NOT NULL, " +
            "parent_type INTEGER NOT NULL, parent_uuid TEXT NOT NULL, " +
            "PRIMARY KEY (uuid)) WITHOUT ROWID; " +
            "CREATE UNIQUE INDEX IF NOT EXISTS dajet_entity_code ON dajet_entity (code);";
        private const string CREATE_PROPERTY_TABLE =
            "CREATE TABLE IF NOT EXISTS dajet_property " +
            "(uuid TEXT NOT NULL, name TEXT NOT NULL, owner TEXT NOT NULL, " +
            "readonly INTEGER NOT NULL DEFAULT 0, column TEXT NOT NULL, " +
            "type TEXT NOT NULL, code INTEGER, size INTEGER, scale INTEGER, " +
            "PRIMARY KEY (uuid)) WITHOUT ROWID;";
        public bool TryConfigureDatabase(out string error)
        {
            error = null;

            List<string> scripts = new()
            {
                CREATE_SEQUENCE_TABLE,
                CREATE_NAMESPACE_TABLE,
                CREATE_ENTITY_TABLE,
                CREATE_PROPERTY_TABLE
            };

            try
            {
                using (SqliteConnection connection = new(_connectionString))
                {
                    connection.Open();

                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        foreach (string script in scripts)
                        {
                            command.CommandText = script;

                            _ = command.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return error is null;
        }
        #endregion
        public int GetNextSequenceValue()
        {
            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = GET_NEXT_SEQUENCE_VALUE;

                    return (int)command.ExecuteScalar();
                }
            }
        }
        public void CreateEntity(in EntityDefinition entity)
        {
            StringBuilder script = new();

            string tableName = $"t{entity.TypeCode}";

            script.Append("CREATE TABLE ").Append(tableName).AppendLine("(");

            for (int i = 0; i < entity.Properties.Count; i++)
            {
                MetadataProperty property = entity.Properties[i];
                
                DataTypeDescriptor type = property.PropertyType;

                if (property.Purpose == PropertyPurpose.System)
                {
                    if (property.Name == "Ссылка")
                    {
                        property.Columns.Add(CreateReferenceColumn(property.DbName, type.TypeCode));
                    }
                    else // ВерсияДанных (row version)
                    {
                        property.Columns.Add(CreateVersionColumn(property.DbName));
                    }
                }
                else if (type.IsUnionType(out _, out _))
                {
                    ConfigureMultipleTypeProperty(in property);
                }
                else // single type value
                {
                    ConfigureSingleTypeProperty(in property);
                }

                foreach (MetadataColumn column in property.Columns)
                {
                    script.Append(column.Name).Append(' ').Append(column.TypeName);
                }
            }

            script.AppendLine(");");
        }
        private void CreateIndex(in EntityDefinition entity) { }
        private MetadataColumn CreateBooleanColumn(in string name)
        {
            return new MetadataColumn()
            {
                Name = $"_{name}_l",
                Purpose = ColumnPurpose.Boolean,
                TypeName = "BLOB NOT NULL DEFAULT X'00'"
            };
        }
        private MetadataColumn CreateNumericColumn(in string name, int precision, int scale, bool signed)
        {
            if (scale == 0)
            {
                return new MetadataColumn()
                {
                    Name = $"_{name}_i",
                    Purpose = ColumnPurpose.Numeric,
                    TypeName = "INTEGER NOT NULL DEFAULT 0"
                };
            }
            else
            {
                return new MetadataColumn()
                {
                    Name = $"_{name}_n",
                    Purpose = ColumnPurpose.Numeric,
                    TypeName = "REAL NOT NULL DEFAULT 0.00"
                };
            }
        }
        private MetadataColumn CreateDateTimeColumn(in string name)
        {
            return new MetadataColumn()
            {
                Name = $"_{name}_t",
                Purpose = ColumnPurpose.DateTime,
                TypeName = "TEXT NOT NULL DEFAULT '0001-01-01T00:00:00'"
            };
        }
        private MetadataColumn CreateStringColumn(in string name, int length)
        {
            return new MetadataColumn()
            {
                Name = $"_{name}_s",
                Purpose = ColumnPurpose.String,
                TypeName = "TEXT NOT NULL DEFAULT ''"
            };
        }
        private MetadataColumn CreateBinaryColumn(in string name, int size)
        {
            return new MetadataColumn()
            {
                Name = $"_{name}_b",
                Purpose = ColumnPurpose.Binary,
                TypeName = "BLOB NULL DEFAULT NULL"
            };
        }
        private MetadataColumn CreateUuidColumn(in string name)
        {
            return new MetadataColumn()
            {
                Name = $"_{name}_u",
                Purpose = ColumnPurpose.Default,
                TypeName = "BLOB NOT NULL DEFAULT X'00000000000000000000000000000000'"
            };
        }
        private MetadataColumn CreateReferenceColumn(in string name, int discriminator)
        {
            string typeCode = discriminator == 0 ? string.Empty : $"_{discriminator}";

            return new MetadataColumn()
            {
                Name = $"_{name}_r{typeCode}",
                Purpose = ColumnPurpose.Identity,
                TypeName = "BLOB NOT NULL DEFAULT X'00000000000000000000000000000000'"
            };
        }
        private MetadataColumn CreateTagColumn(in string name)
        {
            return new MetadataColumn()
            {
                Name = $"_{name}_m",
                Purpose = ColumnPurpose.Tag,
                TypeName = "BLOB NOT NULL DEFAULT X'00000000000000000000000000000000'"
            };
        }
        private MetadataColumn CreateDiscriminatorColumn(in string name)
        {
            return new MetadataColumn()
            {
                Name = $"_{name}_d",
                Purpose = ColumnPurpose.TypeCode,
                TypeName = "BLOB NOT NULL DEFAULT X'00000000'"
            };
        }
        private MetadataColumn CreateVersionColumn(in string name)
        {
            return new MetadataColumn()
            {
                Name = $"_{name}_v",
                Purpose = ColumnPurpose.Default,
                TypeName = "INTEGER NOT NULL DEFAULT 0"
            };
        }
        private void ConfigureSingleTypeProperty(in MetadataProperty property)
        {
            DataTypeDescriptor type = property.PropertyType;

            if (type.IsUnionType(out _, out _)) { return; }

            if (type.IsUuid)
            {
                property.Columns.Add(CreateUuidColumn(property.DbName));
            }
            else if (type.IsBinary || type.IsValueStorage) // synonyms !?
            {
                property.Columns.Add(CreateBinaryColumn(property.DbName,
                    type.StringLength));
            }
            else if (type.CanBeBoolean)
            {
                property.Columns.Add(CreateBooleanColumn(property.DbName));
            }
            else if (type.CanBeNumeric)
            {
                property.Columns.Add(CreateNumericColumn(property.DbName,
                    type.NumericPrecision, type.NumericScale,
                    type.NumericKind == NumericKind.CanBeNegative));
            }
            else if (type.CanBeDateTime)
            {
                property.Columns.Add(CreateDateTimeColumn(property.DbName));
            }
            else if (type.CanBeString)
            {
                property.Columns.Add(CreateStringColumn(property.DbName,
                    type.StringLength));
            }
            else if (type.CanBeReference)
            {
                property.Columns.Add(CreateReferenceColumn(property.DbName,
                    type.TypeCode));
            }
        }
        private void ConfigureMultipleTypeProperty(in MetadataProperty property)
        {
            DataTypeDescriptor type = property.PropertyType;

            if (!type.IsUnionType(out bool canBeSimple, out bool canBeReference))
            {
                return;
            }

            if (canBeSimple)
            {
                property.Columns.Add(CreateTagColumn(property.DbName));

                if (type.CanBeBoolean)
                {
                    property.Columns.Add(CreateBooleanColumn(property.DbName));
                }

                if (type.CanBeNumeric)
                {
                    property.Columns.Add(CreateNumericColumn(property.DbName,
                        type.NumericPrecision, type.NumericScale,
                        type.NumericKind == NumericKind.CanBeNegative));
                }

                if (type.CanBeDateTime)
                {
                    property.Columns.Add(CreateDateTimeColumn(property.DbName));
                }

                if (type.CanBeString)
                {
                    property.Columns.Add(CreateStringColumn(property.DbName,
                        type.StringLength));
                }
            }

            if (canBeReference)
            {
                if (type.TypeCode == 0)
                {
                    property.Columns.Add(CreateDiscriminatorColumn(property.DbName));
                }

                property.Columns.Add(CreateReferenceColumn(property.DbName,
                    type.TypeCode));
            }
        }
    }
}