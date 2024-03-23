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

            for (int i = 0; i < entity.Properties.Count; i++)
            {
                MetadataProperty property = entity.Properties[i];

                DataTypeDescriptor type = property.PropertyType;

                string purpose = string.Empty;
                string definition = string.Empty;
                string columnName = property.DbName;

                if (type.IsUuid) { purpose = "u"; definition = "TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'"; }
                else if (type.IsValueStorage) { purpose = "b"; definition = "BLOB NULL DEFAULT NULL"; }
                else
                {
                    if (type.CanBeBoolean) { purpose = "l"; definition = "INTEGER NOT NULL DEFAULT 0"; }
                    if (type.CanBeNumeric) { purpose = "n"; definition = "REAL NOT NULL DEFAULT 0.00"; }
                    if (type.CanBeDateTime) { purpose = "t"; definition = "INTEGER NOT NULL DEFAULT 0"; }
                    if (type.CanBeString) { purpose = "s"; definition = "TEXT NOT NULL DEFAULT ''"; }
                    if (type.CanBeReference)
                    {
                        if (type.TypeCode > 0)
                        {
                            purpose = $"r_{type.TypeCode}";
                            definition = "TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'";
                        }
                        else
                        {
                            purpose = "d"; definition = "INTEGER NOT NULL"; // entity type discriminator
                            script.Append(columnName).Append('_').Append(purpose).Append(' ').Append(definition);

                            purpose = "r"; definition = "TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'";
                        }
                    }
                }

                script.Append(columnName).Append('_').Append(purpose).Append(' ').Append(definition);
            }

            script.AppendLine(");");
        }
    }
}