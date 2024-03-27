using DaJet.Data;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using static Npgsql.Replication.PgOutput.Messages.RelationMessage;

namespace DaJet.Sqlite
{
    public sealed class SqliteDbConfigurator : IDbConfigurator
    {
        private readonly string _connectionString;
        public SqliteDbConfigurator(in string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            _connectionString = connectionString;
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
            "(uuid TEXT NOT NULL, parent TEXT NOT NULL, name TEXT NOT NULL, " +
            "PRIMARY KEY (uuid)) WITHOUT ROWID;";
        private const string CREATE_ENTITY_TABLE =
            "CREATE TABLE IF NOT EXISTS dajet_entity " +
            "(uuid TEXT NOT NULL, parent_type INTEGER NOT NULL, parent_uuid TEXT NOT NULL, " +
            "name TEXT NOT NULL, code INTEGER NOT NULL, type INTEGER NOT NULL, " +
            "PRIMARY KEY (uuid)) WITHOUT ROWID; " +
            "CREATE UNIQUE INDEX dajet_entity_code ON dajet_entity (code);";
        private const string CREATE_PROPERTY_TABLE =
            "CREATE TABLE IF NOT EXISTS dajet_property " +
            "(uuid TEXT NOT NULL, entity TEXT NOT NULL, name TEXT NOT NULL, " +
            "readonly INTEGER NOT NULL DEFAULT 0, " +
            "PRIMARY KEY (uuid)) WITHOUT ROWID;";
        private const string CREATE_COLUMN_TABLE =
            "CREATE TABLE IF NOT EXISTS dajet_column " +
            "(uuid TEXT NOT NULL, property TEXT NOT NULL, " +
            "name TEXT NOT NULL, type TEXT NOT NULL, " +
            "is_nullable INTEGER NOT NULL DEFAULT 0, " +
            "key_ordinal INTEGER NOT NULL DEFAULT 0, " +
            "is_primary_key INTEGER NOT NULL DEFAULT 0, " +
            "PRIMARY KEY (uuid)) WITHOUT ROWID;";
        public bool TryConfigureDatabase(out string error)
        {
            error = null;

            List<string> scripts = new()
            {
                CREATE_SEQUENCE_TABLE,
                CREATE_NAMESPACE_TABLE,
                CREATE_ENTITY_TABLE,
                CREATE_PROPERTY_TABLE,
                CREATE_COLUMN_TABLE
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
        public void CreateTable(in EntityDefinition entity)
        {
            StringBuilder script = new();

            string tableName = $"t{entity.TypeCode}";

            script.Append("CREATE TABLE ").Append(tableName).AppendLine("(");

            List<MetadataProperty> pk = new();

            for (int i = 0; i < entity.Properties.Count; i++)
            {
                MetadataProperty property = entity.Properties[i];

                if (property.PkOrdinal > 0) { pk.Add(property); }

                DataTypeDescriptor type = property.PropertyType;

                if (property.Purpose == PropertyPurpose.System)
                {
                    if (property.Name == "Ссылка")
                    {
                        property.Columns.Add(new MetadataColumn()
                        {
                            Name = $"_{property.DbName}_r_{type.TypeCode}",
                            Purpose = ColumnPurpose.Identity,
                            KeyOrdinal = property.PkOrdinal,
                            IsPrimaryKey = property.PkOrdinal > 0,
                            TypeName = "TEXT NOT NULL"
                        });
                    }
                    else // ВерсияДанных (row version)
                    {
                        property.Columns.Add(new MetadataColumn()
                        {
                            Name = $"_{property.DbName}_v",
                            Purpose = ColumnPurpose.Default,
                            KeyOrdinal = property.PkOrdinal,
                            IsPrimaryKey = property.PkOrdinal > 0,
                            TypeName = "INTEGER NOT NULL DEFAULT 0"
                        });
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
        private void ConfigureSingleTypeProperty(in MetadataProperty property)
        {
            DataTypeDescriptor type = property.PropertyType;

            if (type.IsUnionType(out _, out _))
            {
                return;
            }

            if (type.IsUuid)
            {
                property.Columns.Add(new MetadataColumn()
                {
                    Name = $"_{property.DbName}_u",
                    Purpose = ColumnPurpose.Default,
                    KeyOrdinal = property.PkOrdinal,
                    IsPrimaryKey = property.PkOrdinal > 0,
                    TypeName = "TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'"
                });
            }
            else if (type.IsBinary || type.IsValueStorage) // synonyms !?
            {
                property.Columns.Add(new MetadataColumn()
                {
                    Name = $"_{property.DbName}_b",
                    Purpose = ColumnPurpose.Default,
                    KeyOrdinal = property.PkOrdinal,
                    IsPrimaryKey = property.PkOrdinal > 0,
                    TypeName = "BLOB"
                });
            }
            else if (type.CanBeBoolean)
            {
                property.Columns.Add(new MetadataColumn()
                {
                    Name = $"_{property.DbName}_l",
                    Purpose = ColumnPurpose.Boolean,
                    KeyOrdinal = property.PkOrdinal,
                    IsPrimaryKey = property.PkOrdinal > 0,
                    TypeName = "INTEGER NOT NULL DEFAULT 0"
                });
            }
            else if (type.CanBeNumeric)
            {
                if (type.NumericScale == 0)
                {
                    property.Columns.Add(new MetadataColumn()
                    {
                        Name = $"_{property.DbName}_n",
                        Purpose = ColumnPurpose.Numeric,
                        KeyOrdinal = property.PkOrdinal,
                        IsPrimaryKey = property.PkOrdinal > 0,
                        TypeName = "INTEGER NOT NULL DEFAULT 0"
                    });
                }
                else
                {
                    property.Columns.Add(new MetadataColumn()
                    {
                        Name = $"_{property.DbName}_n",
                        Purpose = ColumnPurpose.Numeric,
                        KeyOrdinal = property.PkOrdinal,
                        IsPrimaryKey = property.PkOrdinal > 0,
                        TypeName = "REAL NOT NULL DEFAULT 0.00"
                    });
                }
            }
            else if (type.CanBeDateTime)
            {
                property.Columns.Add(new MetadataColumn()
                {
                    Name = $"_{property.DbName}_t",
                    Purpose = ColumnPurpose.DateTime,
                    KeyOrdinal = property.PkOrdinal,
                    IsPrimaryKey = property.PkOrdinal > 0,
                    TypeName = "TEXT NOT NULL DEFAULT '0001-01-01T00:00:00'"
                });
            }
            else if (type.CanBeString)
            {
                property.Columns.Add(new MetadataColumn()
                {
                    Name = $"_{property.DbName}_s",
                    Purpose = ColumnPurpose.String,
                    KeyOrdinal = property.PkOrdinal,
                    IsPrimaryKey = property.PkOrdinal > 0,
                    TypeName = "TEXT NOT NULL DEFAULT ''"
                });
            }
            else if (type.CanBeReference)
            {
                property.Columns.Add(new MetadataColumn()
                {
                    Name = $"_{property.DbName}_r_{type.TypeCode}",
                    Purpose = ColumnPurpose.Identity,
                    KeyOrdinal = property.PkOrdinal,
                    IsPrimaryKey = property.PkOrdinal > 0,
                    TypeName = "TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'"
                });
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
                property.Columns.Add(new MetadataColumn()
                {
                    Name = $"_{property.DbName}_m",
                    Purpose = ColumnPurpose.Tag,
                    KeyOrdinal = property.PkOrdinal,
                    IsPrimaryKey = property.PkOrdinal > 0,
                    Length = 1,
                    TypeName = "BLOB NOT NULL DEFAULT X'01'"
                });

                if (type.CanBeBoolean)
                {
                    property.Columns.Add(new MetadataColumn()
                    {
                        Name = $"_{property.DbName}_l",
                        Purpose = ColumnPurpose.Boolean,
                        KeyOrdinal = property.PkOrdinal,
                        IsPrimaryKey = property.PkOrdinal > 0,
                        TypeName = "INTEGER NOT NULL DEFAULT 0"
                    });
                }

                if (type.CanBeNumeric)
                {
                    if (type.NumericScale == 0)
                    {
                        property.Columns.Add(new MetadataColumn()
                        {
                            Name = $"_{property.DbName}_n",
                            Purpose = ColumnPurpose.Numeric,
                            KeyOrdinal = property.PkOrdinal,
                            IsPrimaryKey = property.PkOrdinal > 0,
                            TypeName = "INTEGER NOT NULL DEFAULT 0"
                        });
                    }
                    else
                    {
                        property.Columns.Add(new MetadataColumn()
                        {
                            Name = $"_{property.DbName}_n",
                            Purpose = ColumnPurpose.Numeric,
                            KeyOrdinal = property.PkOrdinal,
                            IsPrimaryKey = property.PkOrdinal > 0,
                            TypeName = "REAL NOT NULL DEFAULT 0.00"
                        });
                    }
                }

                if (type.CanBeDateTime)
                {
                    property.Columns.Add(new MetadataColumn()
                    {
                        Name = $"_{property.DbName}_t",
                        Purpose = ColumnPurpose.DateTime,
                        KeyOrdinal = property.PkOrdinal,
                        IsPrimaryKey = property.PkOrdinal > 0,
                        TypeName = "TEXT NOT NULL DEFAULT '0001-01-01T00:00:00'"
                    });
                }

                if (type.CanBeString)
                {
                    property.Columns.Add(new MetadataColumn()
                    {
                        Name = $"_{property.DbName}_s",
                        Purpose = ColumnPurpose.String,
                        KeyOrdinal = property.PkOrdinal,
                        IsPrimaryKey = property.PkOrdinal > 0,
                        TypeName = "TEXT NOT NULL DEFAULT ''"
                    });
                }
            }

            if (canBeReference)
            {
                string columnName = $"_{property.DbName}_r";

                if (type.TypeCode == 0)
                {
                    property.Columns.Add(new MetadataColumn()
                    {
                        Name = $"_{property.DbName}_d",
                        Purpose = ColumnPurpose.TypeCode,
                        KeyOrdinal = property.PkOrdinal,
                        IsPrimaryKey = property.PkOrdinal > 0,
                        TypeName = "INTEGER NOT NULL DEFAULT 0"
                    });
                }
                else
                {
                    columnName += $"_{type.TypeCode}";
                }

                property.Columns.Add(new MetadataColumn()
                {
                    Name = columnName,
                    Purpose = ColumnPurpose.Identity,
                    KeyOrdinal = property.PkOrdinal,
                    IsPrimaryKey = property.PkOrdinal > 0,
                    TypeName = "TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'"
                });
            }
        }
    }
}