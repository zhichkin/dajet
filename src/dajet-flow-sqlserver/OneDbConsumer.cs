using DaJet.Data;
using DaJet.Metadata;
using DaJet.Model;
using DaJet.Scripting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Data;

namespace DaJet.Flow.SqlServer
{
    [PipelineBlock] public sealed class OneDbConsumer : SourceBlock<IDataRecord>
    {
        private int _state;
        private const int STATE_IS_IDLE = 0;
        private const int STATE_IS_ACTIVE = 1;
        private const int STATE_DISPOSING = 2;
        private bool CanExecute { get { return Interlocked.CompareExchange(ref _state, STATE_IS_ACTIVE, STATE_IS_IDLE) == STATE_IS_IDLE; } }
        private bool IsActive { get { return Interlocked.CompareExchange(ref _state, STATE_IS_ACTIVE, STATE_IS_ACTIVE) == STATE_IS_ACTIVE; } }
        private bool CanDispose { get { return Interlocked.CompareExchange(ref _state, STATE_DISPOSING, STATE_IS_ACTIVE) == STATE_IS_ACTIVE; } }

        #region "PRIVATE VARIABLES"
        private readonly IPipeline _pipeline;
        private readonly ConsumerOptions _options;
        private readonly IMetadataService _metadata;
        private readonly ScriptDataMapper _scripts;
        private readonly InfoBaseDataMapper _databases;
        private string CommandText { get; set; }
        private string ConnectionString { get; set; }
        private GeneratorResult ScriptGenerator { get; set; }
        private Dictionary<string, object> ScriptParameters { get; set; }
        #endregion
        
        [ActivatorUtilitiesConstructor] public OneDbConsumer(ConsumerOptions options, IPipeline pipeline,
            InfoBaseDataMapper databases, ScriptDataMapper scripts, IMetadataService metadata)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            _scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));
            _databases = databases ?? throw new ArgumentNullException(nameof(databases));
        }
        public override void Execute()
        {
            if (CanExecute)
            {
                try
                {
                    ConfigureConsumer();

                    ExecuteConsumer();
                }
                catch
                {
                    throw;
                }
                finally
                {
                    _Dispose();
                }
            }   
        }
        protected override void _Dispose() { if (CanDispose) { _ = Interlocked.Exchange(ref _state, STATE_IS_IDLE); } }
        private void ConfigureConsumer()
        {
            InfoBaseRecord database = _databases.Select(_options.Source);
            if (database is null) { throw new Exception($"Source not found: {_options.Source}"); }

            ScriptRecord script = _scripts.SelectScriptByPath(database.Uuid, _options.Script);
            if (script is null) { throw new Exception($"Script not found: {_options.Script}"); }

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

            if (_options.Timeout < 0) { _options.Timeout = 10; }
        }
        private void ExecuteConsumer()
        {
            _pipeline.UpdateMonitorStartTime(DateTime.Now);

            int consumed;
            int processed = 0;

            do
            {
                consumed = 0;

                using (SqlConnection connection = new(ConnectionString))
                {
                    connection.Open();

                    SqlCommand command = connection.CreateCommand();
                    SqlTransaction transaction = connection.BeginTransaction();

                    command.Connection = connection;
                    command.Transaction = transaction;
                    command.CommandText = CommandText;
                    command.CommandType = CommandType.Text;
                    command.CommandTimeout = _options.Timeout;

                    foreach (var parameter in ScriptParameters)
                    {
                        command.Parameters.AddWithValue(parameter.Key, parameter.Value);
                    }

                    try
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                _next?.Process(new OneDbDataRecord(reader, ScriptGenerator.Mapper));

                                consumed++;
                                processed++;
                            }
                            reader.Close();
                        }

                        if (consumed > 0) { _next?.Synchronize(); }

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

                _pipeline.UpdateMonitorStatus($"Processed {processed} records");
                _pipeline.UpdateMonitorFinishTime(DateTime.Now);
            }
            while (consumed > 0 && IsActive);
        }
    }
}