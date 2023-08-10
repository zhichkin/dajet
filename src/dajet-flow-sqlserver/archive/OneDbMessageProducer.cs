using DaJet.Metadata;
using DaJet.Options;
using DaJet.Scripting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Data;

namespace DaJet.Flow.SqlServer
{
    // [PipelineBlock]
    public sealed class OneDbMessageProducer : TargetBlock<DbMessage>
    {
        private readonly ScriptDataMapper _scripts;
        private readonly InfoBaseDataMapper _databases;
        private readonly IMetadataService _metadata;
        [Option] public string Target { get; set; } = string.Empty;
        [Option] public string Script { get; set; } = string.Empty;
        [Option] public int Timeout { get; set; } = 10; // seconds (value of 0 indicates no limit)
        [ActivatorUtilitiesConstructor] public OneDbMessageProducer(InfoBaseDataMapper databases, ScriptDataMapper scripts, IMetadataService metadata)
        {
            _scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));
            _databases = databases ?? throw new ArgumentNullException(nameof(databases));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }
        private string CommandText { get; set; }
        private string ConnectionString { get; set; }
        private GeneratorResult ScriptGenerator { get; set; }
        protected override void _Configure()
        {
            InfoBaseModel database = _databases.Select(Target) ?? throw new ArgumentException($"Target not found: {Target}");
            ScriptRecord script = _scripts.SelectScriptByPath(database.Uuid, Script) ?? throw new ArgumentException($"Script not found: {Script}");

            if (!_metadata.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                throw new Exception(error);
            }

            ScriptExecutor executor = new(provider, _metadata, _databases, _scripts);
            ScriptGenerator = executor.PrepareScript(script.Script);

            if (!ScriptGenerator.Success)
            {
                throw new Exception(ScriptGenerator.Error);
            }

            CommandText = ScriptGenerator.Script;
            ConnectionString = database.ConnectionString;

            if (Timeout < 0) { Timeout = 10; }
        }
        public override void Process(in DbMessage input)
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
        private void ConfigureParameters(in SqlCommand command, in DbMessage input)
        {
            command.Parameters.Clear();
            command.Parameters.AddWithValue("sender", input.Sender);
            command.Parameters.AddWithValue("msg_time", input.TimeStamp);
            command.Parameters.AddWithValue("msg_uuid", input.Uuid.ToByteArray());
            command.Parameters.AddWithValue("msg_number", input.Number);
            command.Parameters.AddWithValue("msg_type", input.Type);
            command.Parameters.AddWithValue("msg_body", input.Body);
        }
    }
}