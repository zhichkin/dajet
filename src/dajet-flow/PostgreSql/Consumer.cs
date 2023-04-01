using Npgsql;
using NpgsqlTypes;
using System.Data;
using System.Data.Common;

namespace DaJet.Flow.PostgreSql
{
    public sealed class Consumer : SourceBlock<DbDataReader>, IConfigurable
    {
        private Dictionary<string, string>? _options;
        public Consumer() { }
        public void Configure(in Dictionary<string, string> options)
        {
            _options = options;
        }
        public override void Pump(CancellationToken token)
        {
            int consumed;

            using (NpgsqlConnection connection = new(_options?["ConnectionString"]))
            {
                connection.Open();

                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    ConfigureCommand(command);

                    do
                    {
                        consumed = 0;

                        using (NpgsqlTransaction transaction = connection.BeginTransaction())
                        {
                            command.Transaction = transaction;

                            using (NpgsqlDataReader reader = command.ExecuteReader())
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
            command.CommandText = _options?["SourceScript"];

            command.Parameters.Clear();

            NpgsqlParameter parameter = new("MessagesPerTransaction", NpgsqlDbType.Integer)
            {
                Value = int.Parse(_options?["MessagesPerTransaction"])
            };

            command.Parameters.Add(parameter);
        }
        public override void Dispose()
        {
            // TODO
        }
    }
}