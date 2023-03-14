using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.Common;

namespace DaJet.Data.PostgreSql
{
    public sealed class PgQueryExecutor : QueryExecutor
    {
        public PgQueryExecutor(string connectionString) : base(connectionString) { }
        public override string GetDatabaseName()
        {
            string databaseName = new NpgsqlConnectionStringBuilder(_connectionString).Database!;

            return (string.IsNullOrWhiteSpace(databaseName) ? string.Empty : databaseName);
        }
        protected override DbConnection GetDbConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }
        protected override void ConfigureQueryParameters(in DbCommand command, in Dictionary<string, object> parameters)
        {
            if (command is not NpgsqlCommand _command)
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