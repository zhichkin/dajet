
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data.Common;

namespace DaJet.Data.Sqlite
{
    public sealed class SqliteQueryExecutor : QueryExecutor
    {
        public SqliteQueryExecutor(string connectionString) : base(connectionString) { }
        public override string GetDatabaseName()
        {
            return new SqliteConnectionStringBuilder(_connectionString).DataSource;
        }
        protected override DbConnection GetDbConnection()
        {
            return new SqliteConnection(_connectionString);
        }
        protected override void ConfigureQueryParameters(in DbCommand command, in Dictionary<string, object> parameters)
        {
            if (command is not SqliteCommand _command)
            {
                throw new InvalidOperationException(nameof(command));
            }

            foreach (var parameter in parameters)
            {
                _command.Parameters.AddWithValue(parameter.Key, parameter.Value);
            }
        }
    }
}