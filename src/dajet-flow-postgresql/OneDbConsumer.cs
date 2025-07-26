using DaJet.Data;
using DaJet.Metadata;
using DaJet.Model;
using DaJet.Scripting;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Data;

namespace DaJet.Flow.PostgreSql
{
    public sealed class OneDbConsumer : SourceBlock<DataObject>
    {
        private int _state;
        private const int STATE_IS_IDLE = 0;
        private const int STATE_IS_ACTIVE = 1;
        private const int STATE_DISPOSING = 2;
        private bool CanExecute { get { return Interlocked.CompareExchange(ref _state, STATE_IS_ACTIVE, STATE_IS_IDLE) == STATE_IS_IDLE; } }
        private bool IsActive { get { return Interlocked.CompareExchange(ref _state, STATE_IS_ACTIVE, STATE_IS_ACTIVE) == STATE_IS_ACTIVE; } }
        private bool CanDispose { get { return Interlocked.CompareExchange(ref _state, STATE_DISPOSING, STATE_IS_ACTIVE) == STATE_IS_ACTIVE; } }

        #region "PRIVATE VARIABLES"
        private readonly IDataSource _source;
        private readonly IPipeline _pipeline;
        private readonly ConsumerOptions _options;
        private readonly IMetadataService _metadata;
        private string CommandText { get; set; }
        private string ConnectionString { get; set; }
        private TranspilerResult ScriptInfo { get; set; }
        private Dictionary<string, object> ScriptParameters { get; set; }
        #endregion
        
        [ActivatorUtilitiesConstructor]
        public OneDbConsumer(IDataSource source, IPipeline pipeline, ConsumerOptions options, IMetadataService metadata)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
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
            InfoBaseRecord database = _source.Select<InfoBaseRecord>(_options.Source)
                ?? throw new Exception($"Source not found: {_options.Source}");

            string scriptPath = database.Name + "/" + _options.Script;

            ScriptRecord script = _source.Select<ScriptRecord>(scriptPath)
                ?? throw new Exception($"Script not found: {scriptPath}");

            if (!_metadata.TryGetOrCreate(in database, out IMetadataProvider provider, out string error))
            {
                throw new Exception(error);
            }

            Dictionary<string, object> parameters = new();

            if (!ScriptProcessor.TryProcess(in provider, script.Script, in parameters, out TranspilerResult result, out error))
            {
                throw new Exception(error);
            }

            ScriptInfo = result;
            CommandText = result.SqlScript;
            ScriptParameters = result.Parameters;
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

                using (NpgsqlConnection connection = new(ConnectionString))
                {
                    connection.Open();

                    NpgsqlCommand command = connection.CreateCommand();
                    NpgsqlTransaction transaction = connection.BeginTransaction();

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
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            DataObject record = new(ScriptInfo.Mappers[0].Properties.Count);

                            while (reader.Read())
                            {
                                ScriptInfo.Mappers[0].Map(reader, in record);

                                _next?.Process(in record);

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