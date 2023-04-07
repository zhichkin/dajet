using Npgsql;
using NpgsqlTypes;
using System.Data;
using System.Data.Common;

namespace DaJet.Flow.PostgreSql
{
    [PipelineBlock]
    public sealed class Producer : TargetBlock<DbDataReader>
    {
        public Producer() { }
        [Option] public string CommandText { get; set; } = string.Empty;
        [Option] public string ConnectionString { get; set; } = string.Empty;
        public override void Process(in DbDataReader input)
        {
            using (NpgsqlConnection connection = new(ConnectionString))
            {
                connection.Open();

                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    ConfigureCommand(command, in input);

                    _ = command.ExecuteNonQuery();
                }
            }
        }
        private void ConfigureCommand(in DbCommand command, in DbDataReader input)
        {
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 10; // seconds
            command.CommandText = CommandText;

            command.Parameters.Clear();

            command.Parameters.Add(new NpgsqlParameter("МоментВремени", NpgsqlDbType.Numeric) { Value = input["МоментВремени"] });
            command.Parameters.Add(new NpgsqlParameter("Идентификатор", NpgsqlDbType.Bytea) { Value = input["Идентификатор"] });
            command.Parameters.Add(new NpgsqlParameter("Заголовки", NpgsqlDbType.Varchar) { Value = input["Заголовки"] });
            command.Parameters.Add(new NpgsqlParameter("Отправитель", NpgsqlDbType.Varchar) { Value = input["Отправитель"] });
            command.Parameters.Add(new NpgsqlParameter("ТипСообщения", NpgsqlDbType.Varchar) { Value = input["ТипСообщения"] });
            command.Parameters.Add(new NpgsqlParameter("ТелоСообщения", NpgsqlDbType.Varchar) { Value = input["ТелоСообщения"] });
            command.Parameters.Add(new NpgsqlParameter("ДатаВремя", NpgsqlDbType.Timestamp) { Value = input["ДатаВремя"] });
            command.Parameters.Add(new NpgsqlParameter("ТипОперации", NpgsqlDbType.Varchar) { Value = input["ТипОперации"] });
            command.Parameters.Add(new NpgsqlParameter("ОписаниеОшибки", NpgsqlDbType.Varchar) { Value = string.Empty });
            command.Parameters.Add(new NpgsqlParameter("КоличествоОшибок", NpgsqlDbType.Integer) { Value = 0 });
        }
        protected override void _Synchronize()
        {
            // do nothing
        }
    }
}