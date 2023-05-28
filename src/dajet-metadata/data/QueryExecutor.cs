using DaJet.Data.PostgreSql;
using DaJet.Data.Sqlite;
using DaJet.Data.SqlServer;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace DaJet.Data
{
    public abstract class QueryExecutor : IQueryExecutor
    {
        public static IQueryExecutor Create(DatabaseProvider provider, string connectionString)
        {
            if (provider == DatabaseProvider.SqlServer)
            {
                return new MsQueryExecutor(connectionString);
            }
            else if (provider == DatabaseProvider.PostgreSql)
            {
                return new PgQueryExecutor(connectionString);
            }
            else if (provider == DatabaseProvider.Sqlite)
            {
                return new SqliteQueryExecutor(connectionString);
            }

            return null;
        }
        protected readonly string _connectionString;
        public QueryExecutor(string connectionString) { _connectionString = connectionString; }
        public abstract string GetDatabaseName();
        protected abstract DbConnection GetDbConnection();
        protected abstract void ConfigureQueryParameters(in DbCommand command, in Dictionary<string, object> parameters);
        public T ExecuteScalar<T>(in string script, int timeout)
        {
            T result = default;

            using (DbConnection connection = GetDbConnection())
            {
                connection.Open();

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = script;
                    command.CommandTimeout = timeout; // seconds

                    object value = command.ExecuteScalar();

                    if (value != null)
                    {
                        result = (T)value;
                    }
                }
            }

            return result;
        }
        public void ExecuteNonQuery(in string script, int timeout)
        {
            using (DbConnection connection = GetDbConnection())
            {
                connection.Open();

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = script;
                    command.CommandTimeout = timeout;

                    _ = command.ExecuteNonQuery();
                }
            }
        }
        public void TxExecuteNonQuery(in List<string> scripts, int timeout)
        {
            using (DbConnection connection = GetDbConnection())
            {
                connection.Open();

                using (DbTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    using (DbCommand command = connection.CreateCommand())
                    {
                        command.Connection = connection;
                        command.Transaction = transaction;
                        command.CommandType = CommandType.Text;
                        command.CommandTimeout = timeout;

                        try
                        {
                            foreach (string script in scripts)
                            {
                                command.CommandText = script;

                                _ = command.ExecuteNonQuery();
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
        }
        public IEnumerable<IDataReader> ExecuteReader(string script, int timeout)
        {
            using (DbConnection connection = GetDbConnection())
            {
                connection.Open();

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = script;
                    command.CommandTimeout = timeout;

                    using (IDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            yield return reader;
                        }
                        reader.Close();
                    }
                }
            }
        }
        public IEnumerable<IDataReader> ExecuteReader(string script, int timeout, Dictionary<string, object> parameters)
        {
            using (DbConnection connection = GetDbConnection())
            {
                connection.Open();

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = script;
                    command.CommandTimeout = timeout;

                    ConfigureQueryParameters(in command, in parameters);

                    using (IDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            yield return reader;
                        }
                        reader.Close();
                    }
                }
            }
        }
    }
}