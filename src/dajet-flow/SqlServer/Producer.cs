using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;

namespace DaJet.Flow.SqlServer
{
    public sealed class Producer : TargetBlock<DbDataReader>
    {
        private SqlConnection _connection;
        private SqlCommand _command;
        public Producer() { }
        [Option] public string CommandText { get; set; } = string.Empty;
        [Option] public string ConnectionString { get; set; } = string.Empty;
        public override void Process(in DbDataReader input)
        {
            using (SqlConnection connection = new(ConnectionString))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
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

            command.Parameters.Add(new SqlParameter("МоментВремени", SqlDbType.Decimal) { Value = input["МоментВремени"] });
            command.Parameters.Add(new SqlParameter("Идентификатор", SqlDbType.Binary) { Value = input["Идентификатор"] });
            command.Parameters.Add(new SqlParameter("Заголовки", SqlDbType.NVarChar) { Value = input["Заголовки"] });
            command.Parameters.Add(new SqlParameter("Отправитель", SqlDbType.NVarChar) { Value = input["Отправитель"] });
            command.Parameters.Add(new SqlParameter("ТипСообщения", SqlDbType.NVarChar) { Value = input["ТипСообщения"] });
            command.Parameters.Add(new SqlParameter("ТелоСообщения", SqlDbType.NVarChar) { Value = input["ТелоСообщения"] });
            command.Parameters.Add(new SqlParameter("ДатаВремя", SqlDbType.DateTime2) { Value = input["ДатаВремя"] });
            command.Parameters.Add(new SqlParameter("ТипОперации", SqlDbType.NVarChar) { Value = input["ТипОперации"] });
            command.Parameters.Add(new SqlParameter("ОписаниеОшибки", SqlDbType.NVarChar) { Value = string.Empty });
            command.Parameters.Add(new SqlParameter("КоличествоОшибок", SqlDbType.Int) { Value = 0 });
        }
        protected override void _Synchronize()
        {
            _command?.Dispose();
            _command = null;

            _connection?.Dispose();
            _connection = null;
        }
        private void ProcessTurbo(in DbDataReader input)
        {
            if (_connection == null)
            {
                _connection = new SqlConnection(ConnectionString);
            }

            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }

            if (_command == null)
            {
                _command = _connection.CreateCommand();
            }

            ConfigureCommand(_command, in input);

            _ = _command.ExecuteNonQuery();
        }
    }
}