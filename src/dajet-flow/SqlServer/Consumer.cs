using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;

namespace DaJet.Flow.SqlServer
{
    public sealed class Consumer : SourceBlock<DbDataReader>, IConfigurable
    {
        private Dictionary<string, string> _options;
        public Consumer() { }
        public void Configure(in Dictionary<string, string> options)
        {
            _options = options;
        }
        public override void Execute()
        {
            int consumed;

            using (SqlConnection connection = new(_options?["ConnectionString"]))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    ConfigureCommand(command);

                    do
                    {
                        consumed = 0;

                        using (SqlTransaction transaction = connection.BeginTransaction())
                        {
                            command.Transaction = transaction;

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    _next?.Process(reader);
                                    consumed++;
                                }
                                reader.Close();
                            }
                            if (consumed > 0)
                            {
                                _next?.Synchronize();
                                transaction.Commit();
                            }
                        }
                    } while (consumed > 0);
                }
            }
        }
        private void ConfigureCommand(in DbCommand command)
        {
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 60; // seconds
            command.CommandText = _options?["CommandText"];

            command.Parameters.Clear();

            SqlParameter parameter = new("MessagesPerTransaction", SqlDbType.Int)
            {
                Value = int.Parse(_options?["MessagesPerTransaction"])
            };

            command.Parameters.Add(parameter);
        }
    }
}