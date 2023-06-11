using DaJet.Metadata;
using DaJet.Options;
using DaJet.Scripting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Data;

namespace DaJet.Flow.SqlServer
{
    [PipelineBlock] public sealed class OneDbMonitor : SourceBlock<IDataRecord>
    {
        #region "PRIVATE VARIABLES"
        private readonly IPipelineManager _manager;
        private readonly IMetadataService _metadata;
        private readonly ScriptDataMapper _scripts;
        private readonly InfoBaseDataMapper _databases;
        private string CommandText { get; set; }
        private string ConnectionString { get; set; }
        private GeneratorResult ScriptGenerator { get; set; }
        private Dictionary<string, object> ScriptParameters { get; set; }
        #endregion
        [Option] public Guid Pipeline { get; set; } = Guid.Empty;
        [Option] public string Source { get; set; } = string.Empty;
        [Option] public string Script { get; set; } = string.Empty;
        [ActivatorUtilitiesConstructor] public OneDbMonitor(
            InfoBaseDataMapper databases, ScriptDataMapper scripts, IPipelineManager manager, IMetadataService metadata)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            _scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));
            _databases = databases ?? throw new ArgumentNullException(nameof(databases));
        }
        protected override void _Configure()
        {
            InfoBaseModel database = _databases.Select(Source);
            if (database is null) { throw new Exception($"Source not found: {Source}"); }

            ScriptRecord script = _scripts.SelectScriptByPath(database.Uuid, Script);
            if (script is null) { throw new Exception($"Script not found: {Script}"); }

            if (!_metadata.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                throw new Exception(error);
            }

            ScriptExecutor executor = new(provider, _metadata, _databases, _scripts);
            ScriptGenerator = executor.PrepareScript(script.Script);
            ScriptParameters = executor.Parameters;

            if (!ScriptGenerator.Success)
            {
                throw new Exception(ScriptGenerator.Error);
            }

            CommandText = ScriptGenerator.Script;
            ConnectionString = database.ConnectionString;
        }
        public override void Execute()
        {
            using (SqlConnection connection = new(ConnectionString))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    ConfigureCommand(in command);

                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        command.Transaction = transaction;

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Process(in reader);
                            }
                            reader.Close();
                        }

                        transaction.Commit();
                    }
                }
            }
        }
        private void ConfigureCommand(in SqlCommand command)
        {
            command.CommandType = CommandType.Text;
            command.CommandText = CommandText;
            command.CommandTimeout = 60; // seconds

            command.Parameters.Clear();

            foreach (var parameter in ScriptParameters)
            {
                command.Parameters.AddWithValue(parameter.Key, parameter.Value);
            }
        }
        private void Process(in SqlDataReader reader)
        {
            DbQueueMonitor info = ScriptGenerator.Mapper.Map<DbQueueMonitor>(reader);

            _manager.UpdatePipelineStatus(Pipeline,
                $"[{info.TimeStamp:yyyy-MM-dd HH:mm:ss}] {info.MessageType} ({info.MessageCount} x {info.DataSizeAvg} = {info.DataSizeSum})");
        }
    }
}