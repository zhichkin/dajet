using DaJet.Data;
using DaJet.Data.Sqlite;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Sqlite;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DaJet.Metadata.Sqlite
{
    public sealed class SqliteMetadataProvider : IMetadataProvider
    {
        // CREATE TABLE sqlite_schema (type text, name text, tbl_name text, rootpage integer, sql text);
        private const string TABLE_EXISTS_COMMAND = "SELECT 1 FROM sqlite_schema WHERE name = @table_name;";
        private const string SELECT_TABLE_COLUMNS = "SELECT c.name, c.type, c.pk, c.notnull " +
            "FROM sqlite_schema AS s " +
            "INNER JOIN pragma_table_info(s.name) AS c " +
            "WHERE s.type = 'table' AND s.name = @table_name " +
            "ORDER BY c.cid ASC;";

        private readonly string _connectionString;
        public SqliteMetadataProvider(in string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            _connectionString = connectionString;
        }
        public int YearOffset { get { return 0; } }
        public string ConnectionString { get { return _connectionString; } }
        public DatabaseProvider DatabaseProvider { get { return DatabaseProvider.Sqlite; } }
        public IQueryExecutor CreateQueryExecutor() { return new SqliteQueryExecutor(in _connectionString); }
        public IDbConfigurator GetDbConfigurator() { return new SqliteDbConfigurator(in _connectionString); }
        public MetadataItem GetMetadataItem(int typeCode) { throw new NotImplementedException(); }
        public IEnumerable<MetadataItem> GetMetadataItems(Guid type) { throw new NotImplementedException(); }
        public MetadataObject GetMetadataObject(Guid type, Guid uuid) { throw new NotImplementedException(); }
        public bool TryGetExtendedInfo(Guid uuid, out MetadataItemEx info) { throw new NotImplementedException(); }
        public bool TryGetEnumValue(in string identifier, out EnumValue value)
        {
            value = null;
            return false;
        }
        public MetadataObject GetMetadataObject(string metadataName)
        {
            if (string.IsNullOrWhiteSpace(metadataName))
            {
                throw new ArgumentNullException(nameof(metadataName));
            }

            string[] identifiers = metadataName.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            string tableName;

            if (identifiers.Length == 1) { tableName = identifiers[0]; }
            else if (identifiers.Length == 2) { tableName = identifiers[1]; }
            else if (identifiers.Length == 3) { tableName = string.Format("{0}_{1}", identifiers[1], identifiers[2]); }
            else { tableName = string.Empty; }

            if (!TableExists(in tableName)) { return null; }

            List<MetadataColumn> columns = SelectTableColumns(in tableName);

            if (columns.Count == 0) { return null; }

            MetadataColumn identity = columns.Where(c => c.IsPrimaryKey && c.Name == "identity").FirstOrDefault();

            ApplicationObject type = identity is null ? new TableDefinition() : new EntityDefinition();

            type.Name = tableName;
            type.TableName = tableName;

            StringSplitOptions splitOptions = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;

            MetadataProperty property;

            foreach (MetadataColumn info in columns)
            {
                string[] qualifiers = info.Name.Split('_', splitOptions);

                int typeCode = 0;
                string name = qualifiers[0];
                char purpose = qualifiers[1][0];

                if (qualifiers.Length == 3)
                {
                    typeCode = int.Parse(qualifiers[2]);
                }

                property = null;

                for (int i = 0; i < type.Properties.Count; i++)
                {
                    if (type.Properties[i].Name == name)
                    {
                        property = type.Properties[i]; break;
                    }
                }

                if (property is null)
                {
                    property = new MetadataProperty()
                    {
                        Name = name
                    };
                    type.Properties.Add(property);
                }

                MetadataColumn column = new()
                {
                    Name = info.Name,
                    TypeName = info.TypeName,
                    Precision = info.Precision,
                    Scale = info.Scale,
                    IsNullable = info.IsNullable
                };

                property.Columns.Add(column);

                if (purpose == 'L') { property.PropertyType.CanBeBoolean = true; }
                else if (purpose == 'N') { property.PropertyType.CanBeNumeric = true; }
                else if (purpose == 'T') { property.PropertyType.CanBeDateTime = true; }
                else if (purpose == 'S') { property.PropertyType.CanBeString = true; }
                else if (purpose == 'B') { property.PropertyType.IsValueStorage = true; }
                else if (purpose == 'U') { property.PropertyType.IsUuid = true; }
                else if (purpose == 'R')
                {
                    property.PropertyType.CanBeReference = true;

                    if (typeCode == 0)
                    {
                        column.Purpose = ColumnPurpose.Identity;
                        property.PropertyType.TypeCode = 0;
                        property.PropertyType.Reference = Guid.Empty;
                    }
                    else
                    {
                        //MetadataItem item = _provider.GetMetadataItem(typeCode);
                        //property.PropertyType.TypeCode = typeCode;
                        //property.PropertyType.Reference = item.Uuid;
                    }
                }
                else if (purpose == 'C')
                {
                    column.Purpose = ColumnPurpose.TypeCode;
                    property.PropertyType.TypeCode = 0;
                    property.PropertyType.Reference = Guid.Empty;
                    property.PropertyType.CanBeReference = true;
                }
            }

            return type;
        }
        private bool TableExists(in string identifier)
        {
            bool exists = false;

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = TABLE_EXISTS_COMMAND;

                    command.Parameters.AddWithValue("table_name", identifier);

                    exists = (int)command.ExecuteScalar() == 1;
                }
            }

            return exists;
        }
        private List<MetadataColumn> SelectTableColumns(in string identifier)
        {
            List<MetadataColumn> columns = new();

            using (SqliteConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_TABLE_COLUMNS;

                    command.Parameters.AddWithValue("table_name", identifier);

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            MetadataColumn column = new()
                            {
                                Name       = reader.GetString(0),    // name
                                TypeName   = reader.GetString(1),    // type
                                KeyOrdinal = reader.GetInt32(2),     // pk
                                IsNullable = reader.GetInt32(3) == 0 // notnull
                            };
                            column.IsPrimaryKey = (column.KeyOrdinal > 0);

                            columns.Add(column);
                        }
                        reader.Close();
                    }
                }
                
            }

            return columns;
        }
    }
}