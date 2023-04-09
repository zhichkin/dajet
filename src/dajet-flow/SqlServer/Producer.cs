using DaJet.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Flow.SqlServer
{
    [PipelineBlock] public sealed class Producer : TargetBlock<Dictionary<string, object>>
    {
        private SqlCommand _command;
        private SqlConnection _connection;
        private readonly ILogger _logger;
        [Option] public Guid Pipeline { get; set; } = Guid.Empty;
        [Option] public string CommandText { get; set; } = string.Empty;
        [Option] public string ConnectionString { get; set; } = string.Empty;
        [ActivatorUtilitiesConstructor] public Producer(ILogger<Producer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        public override void Process(in Dictionary<string, object> input)
        {
            foreach (var item in input)
            {
                if (item.Value is Entity value)
                {
                    input[item.Key] = value.ToString();
                }
            }

            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
            string json = JsonSerializer.Serialize(input, options);

            _logger.LogInformation(json);

            //using (SqlConnection connection = new(ConnectionString))
            //{
            //    connection.Open();

            //    using (SqlCommand command = connection.CreateCommand())
            //    {
            //        ConfigureCommand(command, in input);

            //        _ = command.ExecuteNonQuery();
            //    }
            //}

            
        }
        private void ConfigureCommand(in SqlCommand command, in DbDataReader input)
        {
            command.CommandType = CommandType.Text;
            command.CommandText = CommandText;
            command.CommandTimeout = 10; // seconds

            //Dictionary<string, object> parameters = await ParseScriptParametersFromBody();

            //if (parameters != null)
            //{
            //    foreach (var parameter in parameters)
            //    {
            //        executor.Parameters.Add(parameter.Key, parameter.Value);
            //    }
            //}

            //command.Parameters.Clear();

            //command.Parameters.Add(new SqlParameter("МоментВремени", SqlDbType.Decimal) { Value = input["МоментВремени"] });
            //command.Parameters.Add(new SqlParameter("Идентификатор", SqlDbType.Binary) { Value = input["Идентификатор"] });
            //command.Parameters.Add(new SqlParameter("Заголовки", SqlDbType.NVarChar) { Value = input["Заголовки"] });
            //command.Parameters.Add(new SqlParameter("Отправитель", SqlDbType.NVarChar) { Value = input["Отправитель"] });
            //command.Parameters.Add(new SqlParameter("ТипСообщения", SqlDbType.NVarChar) { Value = input["ТипСообщения"] });
            //command.Parameters.Add(new SqlParameter("ТелоСообщения", SqlDbType.NVarChar) { Value = input["ТелоСообщения"] });
            //command.Parameters.Add(new SqlParameter("ДатаВремя", SqlDbType.DateTime2) { Value = input["ДатаВремя"] });
            //command.Parameters.Add(new SqlParameter("ТипОперации", SqlDbType.NVarChar) { Value = input["ТипОперации"] });
            //command.Parameters.Add(new SqlParameter("ОписаниеОшибки", SqlDbType.NVarChar) { Value = string.Empty });
            //command.Parameters.Add(new SqlParameter("КоличествоОшибок", SqlDbType.Int) { Value = 0 });
        }
        protected override void _Synchronize()
        {
            _command?.Dispose();
            _command = null;

            _connection?.Dispose();
            _connection = null;
        }
    }
}