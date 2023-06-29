using DaJet.Flow;
using DaJet.Metadata;
using DaJet.Options;
using DaJet.Scripting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Data;

namespace DaJet.Exchange.SqlServer
{
    [PipelineBlock] public sealed class OneDbProducer : TargetBlock<OneDbMessage>
    {
        private readonly ScriptDataMapper _scripts;
        private readonly InfoBaseDataMapper _databases;
        private readonly IMetadataService _metadata;
        [Option] public string Target { get; set; } = string.Empty;
        [Option] public string Script { get; set; } = string.Empty;
        [Option] public int Timeout { get; set; } = 10; // seconds (value of 0 indicates no limit)
        [ActivatorUtilitiesConstructor] public OneDbProducer(InfoBaseDataMapper databases, ScriptDataMapper scripts, IMetadataService metadata)
        {
            _scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));
            _databases = databases ?? throw new ArgumentNullException(nameof(databases));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }
        private int YearOffset { get; set; }
        private string CommandText { get; set; }
        private string ConnectionString { get; set; }
        private GeneratorResult ScriptGenerator { get; set; }
        protected override void _Configure()
        {
            if (Timeout < 0) { Timeout = 10; }

            InfoBaseModel database = _databases.Select(Target) ?? throw new ArgumentException($"Target not found: {Target}");
            ScriptRecord script = _scripts.SelectScriptByPath(database.Uuid, Script) ?? throw new ArgumentException($"Script not found: {Script}");

            if (!_metadata.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                throw new Exception(error);
            }

            YearOffset = provider.YearOffset;

            ScriptExecutor executor = new(provider, _metadata, _databases, _scripts);
            ScriptGenerator = executor.PrepareScript(script.Script);

            if (!ScriptGenerator.Success)
            {
                throw new Exception(ScriptGenerator.Error);
            }

            CommandText = ScriptGenerator.Script;
            ConnectionString = database.ConnectionString;
        }
        public override void Process(in OneDbMessage input)
        {
            using (SqlConnection connection = new(ConnectionString))
            {
                connection.Open();

                SqlCommand command = connection.CreateCommand();
                SqlTransaction transaction = connection.BeginTransaction();

                command.Connection = connection;
                command.Transaction = transaction;
                command.CommandText = CommandText;
                command.CommandType = CommandType.Text;
                command.CommandTimeout = Timeout;

                try
                {
                    ConfigureParameters(in command, in input);

                    _ = command.ExecuteNonQuery();

                    transaction.Commit();
                }
                catch (Exception error)
                {
                    try
                    {
                        transaction.Rollback(); throw;
                    }
                    catch
                    {
                        throw error;
                    }
                }
            }
        }
        private void ConfigureParameters(in SqlCommand command, in OneDbMessage input)
        {
            command.Parameters.Clear();
            command.Parameters.AddWithValue("msg_source", input.Sender);
            command.Parameters.AddWithValue("msg_target", string.Join(',', input.Subscribers));
            command.Parameters.AddWithValue("msg_time", DateTime.Now.AddYears(YearOffset));
            command.Parameters.AddWithValue("msg_number", input.Sequence);
            command.Parameters.AddWithValue("msg_type", input.TypeName);
            command.Parameters.AddWithValue("msg_body", input.Payload);
        }
    }
}