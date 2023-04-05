using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;

namespace DaJet.Flow.SqlServer
{
    public sealed class Consumer : SourceBlock<DbDataReader>
    {
        public Consumer() { }
        [Option] public string ConnectionString { get; set; } = string.Empty;
        [Option] public string CommandText { get; set; } = string.Empty;
        [Option] public int MessagesPerTransaction { get; set; } = 1000;
        public override void Execute()
        {
            int consumed;

            using (SqlConnection connection = new(ConnectionString))
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
            command.CommandText = CommandText;

            command.Parameters.Clear();

            SqlParameter parameter = new(nameof(MessagesPerTransaction), SqlDbType.Int)
            {
                Value = MessagesPerTransaction
            };

            command.Parameters.Add(parameter);
        }
    }
}