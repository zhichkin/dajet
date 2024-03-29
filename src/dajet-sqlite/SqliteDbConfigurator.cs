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
        private readonly string _databaseFileFullPath;
        private readonly IDataSource _source;
        public SqliteDbConfigurator(in string databaseFileFullPath)
        {
            if (string.IsNullOrWhiteSpace(databaseFileFullPath))
            {
                throw new ArgumentNullException(nameof(databaseFileFullPath));
            }

            _databaseFileFullPath = databaseFileFullPath;

            _connectionString = new SqliteConnectionStringBuilder()
            {
                DataSource = databaseFileFullPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }
            .ToString();

            _source = new MetadataSource(in _databaseFileFullPath);
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
            "(uuid TEXT NOT NULL, name TEXT NOT NULL, parent TEXT NOT NULL, " +
            "PRIMARY KEY (uuid)) WITHOUT ROWID;";
        private const string CREATE_ENTITY_TABLE =
            "CREATE TABLE IF NOT EXISTS dajet_entity " +
            "(uuid TEXT NOT NULL, type INTEGER NOT NULL, " +
            "code INTEGER NOT NULL, name TEXT NOT NULL, " +
            "table_name TEXT NOT NULL, " +
            "parent_type INTEGER NOT NULL, parent_uuid TEXT NOT NULL, " +
            "PRIMARY KEY (uuid)) WITHOUT ROWID; " +
            "CREATE UNIQUE INDEX IF NOT EXISTS dajet_entity_code ON dajet_entity (code);";
        private const string CREATE_PROPERTY_TABLE =
            "CREATE TABLE IF NOT EXISTS dajet_property " +
            "(uuid TEXT NOT NULL, owner TEXT NOT NULL, name TEXT NOT NULL, " +
            "readonly INTEGER NOT NULL DEFAULT 0, column_name TEXT NOT NULL, " +
            "type TEXT NOT NULL, " + // l,n,t,s,b,u,r
            "length INTEGER NOT NULL DEFAULT 0, fixed INTEGER NOT NULL DEFAULT 0, " +
            "precision INTEGER NOT NULL DEFAULT 4, scale INTEGER NOT NULL DEFAULT 0, signed INTEGER NOT NULL DEFAULT 1, " +
            "discriminator INTEGER NOT NULL DEFAULT 0, primary_key INTEGER NOT NULL DEFAULT 0, " +
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
        public void CreateTable(in EntityDefinition entity)
        {
            StringBuilder script = new();

            string tableName = $"t{entity.TypeCode}";

            script.Append("CREATE TABLE ").Append(tableName).AppendLine("(");

            for (int i = 0; i < entity.Properties.Count; i++)
            {
                MetadataProperty property = entity.Properties[i];

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
        private MetadataColumn CreateStringColumn(in string name, int length, bool variable)
        {
            return new MetadataColumn()
            {
                Name = $"_{name}_s",
                Purpose = ColumnPurpose.String,
                TypeName = "TEXT NOT NULL DEFAULT ''"
            };
        }
        private MetadataColumn CreateBinaryColumn(in string name, int length, bool variable)
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
        private void ConfigureSystemProperty(in MetadataProperty property)
        {
            if (property.Name == "Ссылка")
            {
                property.Columns.Add(CreateReferenceColumn(property.DbName, property.PropertyType.TypeCode));
            }
            else if (property.Name == "ВерсияДанных") // row version
            {
                property.Columns.Add(CreateVersionColumn(property.DbName));
            }
        }
        private void ConfigureSingleTypeProperty(in MetadataProperty property)
        {
            DataTypeDescriptor type = property.PropertyType;

            if (type.IsUuid)
            {
                property.Columns.Add(CreateUuidColumn(property.DbName));
            }
            else if (type.IsBinary || type.IsValueStorage) // synonyms !?
            {
                property.Columns.Add(CreateBinaryColumn(property.DbName,
                    type.StringLength, type.StringKind == StringKind.Variable));
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
                    type.StringLength, type.StringKind == StringKind.Variable));
            }
            else if (type.CanBeReference)
            {
                property.Columns.Add(CreateReferenceColumn(property.DbName, type.TypeCode));
            }
        }
        private void ConfigureMultipleTypeProperty(in MetadataProperty property, bool canBeSimple, bool canBeReference)
        {
            DataTypeDescriptor type = property.PropertyType;

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
                        type.StringLength,
                        type.StringKind == StringKind.Variable));
                }
            }

            if (canBeReference)
            {
                if (type.TypeCode == 0)
                {
                    property.Columns.Add(CreateDiscriminatorColumn(property.DbName));
                }

                property.Columns.Add(CreateReferenceColumn(property.DbName, type.TypeCode));
            }
        }

        private EntityDefinition Convert(in EntityRecord record)
        {
            EntityDefinition definition = new()
            {
                Uuid = record.Identity,
                Name = record.Name,
                TypeCode = record.Code,
                TableName = record.Table
            };

            IDataSource source = new MetadataSource(in _databaseFileFullPath);

            IEnumerable<PropertyRecord> list = source.Query<PropertyRecord>(record.GetEntity());

            if (list is null) { return definition; }

            foreach (PropertyRecord item in list)
            {
                MetadataProperty property = new()
                {
                    Uuid = item.Identity,
                    Name = item.Name,
                    DbName = item.Column,
                    PrimaryKey = item.PrimaryKey,
                    IsDbGenerated = item.IsReadOnly,
                    Purpose = item.Name == "Ссылка" || item.Name == "ВерсияДанных"
                    ? PropertyPurpose.System
                    : PropertyPurpose.Property, 
                };

                // type qualifiers
                property.PropertyType.TypeCode = item.Discriminator;
                property.PropertyType.DateTimePart = DateTimePart.DateTime;
                property.PropertyType.StringLength = item.Length;
                property.PropertyType.StringKind = item.IsFixed ? StringKind.Fixed : StringKind.Variable;
                property.PropertyType.NumericPrecision = item.Precision;
                property.PropertyType.NumericScale = item.Scale;
                property.PropertyType.NumericKind = item.IsSigned ? NumericKind.CanBeNegative : NumericKind.AlwaysPositive;

                if (item.Discriminator == record.TypeCode) // self-reference
                {
                    property.PropertyType.Reference = record.Identity;
                }
                else
                {
                    EntityRecord entity = _source.Select<EntityRecord>(item.Discriminator);

                    if (entity is not null)
                    {
                        property.PropertyType.Reference = entity.Identity;
                    }
                }

                // single types
                property.PropertyType.IsUuid = item.Type.Contains('u');
                property.PropertyType.IsBinary = item.Type.Contains('b');

                if (property.PropertyType.IsUndefined)
                {
                    // multiple types (union)
                    property.PropertyType.CanBeBoolean = item.Type.Contains('l');
                    property.PropertyType.CanBeNumeric = item.Type.Contains('n');
                    property.PropertyType.CanBeDateTime = item.Type.Contains('t');
                    property.PropertyType.CanBeString = item.Type.Contains('s');
                    property.PropertyType.CanBeReference = item.Type.Contains('r');
                }
                
                if (property.Purpose == PropertyPurpose.System)
                {
                    ConfigureSystemProperty(in property);
                }
                else if (property.PropertyType.IsUnionType(out bool canBeSimple, out bool canBeReference))
                {
                    ConfigureMultipleTypeProperty(in property, canBeSimple, canBeReference);
                }
                else
                {
                    ConfigureSingleTypeProperty(in property);
                }

                definition.Properties.Add(property);
            }

            return definition;
        }
    }
}