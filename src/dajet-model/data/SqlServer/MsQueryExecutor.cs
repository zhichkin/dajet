using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data.Common;

namespace DaJet.Data.SqlServer
{
    public sealed class MsQueryExecutor : QueryExecutor
    {
        public MsQueryExecutor(string connectionString) : base(connectionString) { }
        public override string GetDatabaseName()
        {
            return new SqlConnectionStringBuilder(_connectionString).InitialCatalog;
        }
        protected override DbConnection GetDbConnection()
        {
            return new SqlConnection(_connectionString);
        }
        protected override void ConfigureQueryParameters(in DbCommand command, in Dictionary<string, object> parameters)
        {
            if (command is not SqlCommand _command)
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