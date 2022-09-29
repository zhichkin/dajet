using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;

namespace DaJet.Pipeline.SqlServer
{
    public sealed class Producer : TargetBlock<DbDataReader>, IConfigurable
    {
        private Dictionary<string, string>? _options;
        public Producer() { }
        public void Configure(in Dictionary<string, string> options)
        {
            _options = options;
        }
        public override void Process(in DbDataReader input)
        {
            using (SqlConnection connection = new(_options?["ConnectionString"]))
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
            command.CommandText = _options?["TargetScript"];

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
            // do nothing
        }
    }
}