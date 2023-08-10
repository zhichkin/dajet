using DaJet.Data;
using DaJet.Metadata;
using DaJet.Options;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Data;
using System.Text;

namespace DaJet.Flow.PostgreSql
{
    // [PipelineBlock]
    public sealed class OneDbStream : SourceBlock<IDataRecord>
    {
        private int _state;
        private const int STATE_IS_IDLE = 0;
        private const int STATE_IS_ACTIVE = 1;
        private const int STATE_DISPOSING = 2;
        private bool CanExecute { get { return Interlocked.CompareExchange(ref _state, STATE_IS_ACTIVE, STATE_IS_IDLE) == STATE_IS_IDLE; } }
        private bool IsActive { get { return Interlocked.CompareExchange(ref _state, STATE_IS_ACTIVE, STATE_IS_ACTIVE) == STATE_IS_ACTIVE; } }
        private bool CanDispose { get { return Interlocked.CompareExchange(ref _state, STATE_DISPOSING, STATE_IS_ACTIVE) == STATE_IS_ACTIVE; } }

        #region "PRIVATE VARIABLES"
        private readonly IPipelineManager _manager;
        private readonly IMetadataService _metadata;
        private readonly ScriptDataMapper _scripts;
        private readonly InfoBaseDataMapper _databases;
        private string CommandText { get; set; }
        private string ConnectionString { get; set; }
        private List<ScriptCommand> ScriptCommands { get; set; }
        private Dictionary<string, object> ScriptParameters { get; set; }
        #endregion
        [Option] public Guid Pipeline { get; set; } = Guid.Empty;
        [Option] public string Source { get; set; } = string.Empty;
        [Option] public string Script { get; set; } = string.Empty;
        [Option] public int Timeout { get; set; } = 10; // seconds (value of 0 indicates no limit)
        [ActivatorUtilitiesConstructor] public OneDbStream(InfoBaseDataMapper databases, ScriptDataMapper scripts, IPipelineManager manager, IMetadataService metadata)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
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
            InfoBaseModel database = _databases.Select(Source);
            if (database is null) { throw new Exception($"Source not found: {Source}"); }

            ScriptRecord script = _scripts.SelectScriptByPath(database.Uuid, Script);
            if (script is null) { throw new Exception($"Script not found: {Script}"); }

            if (!_metadata.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                throw new Exception(error);
            }

            ScriptExecutor executor = new(provider, _metadata, _databases, _scripts);
            executor.PrepareScript(script.Script, out ScriptModel _, out List<ScriptCommand> commands);
            ScriptCommands = commands;
            ScriptParameters = executor.Parameters;

            CommandText = string.Empty;
            ConnectionString = database.ConnectionString;

            if (Timeout < 0) { Timeout = 10; }
        }
        private void ExecuteConsumer()
        {
            _manager.UpdatePipelineStartTime(Pipeline, DateTime.Now);

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
                    command.CommandTimeout = Timeout;

                    foreach (var parameter in ScriptParameters)
                    {
                        command.Parameters.AddWithValue(parameter.Key, parameter.Value);
                    }

                    try
                    {
                        if (IsSingleReader())
                        {
                            Stream(in command);
                        }
                        else
                        {
                            Throttle(in command);
                        }

                        _next?.Synchronize();

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

                _manager.UpdatePipelineStatus(Pipeline, $"Processed {processed} records");
                _manager.UpdatePipelineFinishTime(Pipeline, DateTime.Now);
            }
            while (consumed > 0 && IsActive);
        }
        private bool IsSingleReader()
        {
            int count = 0;

            foreach (ScriptCommand command in ScriptCommands)
            {
                if (command.Mapper.Properties.Count > 0)
                {
                    count++;
                }
            }

            return (count == 1);
        }
        private string CreateHeadScript()
        {
            StringBuilder script = new();

            for (int i = 0; i < ScriptCommands.Count; i++)
            {
                ScriptCommand command = ScriptCommands[i];

                if (string.IsNullOrEmpty(command.Script))
                {
                    continue;
                }

                script.AppendLine(command.Script);

                if (command.Mapper.Properties.Count > 0)
                {
                    break;
                }
            }

            return script.ToString();
        }
        private string CreateBodyScript()
        {
            StringBuilder script = new();

            bool is_body = false;

            for (int i = 0; i < ScriptCommands.Count; i++)
            {
                ScriptCommand command = ScriptCommands[i];

                if (string.IsNullOrEmpty(command.Script))
                {
                    continue;
                }

                if (is_body)
                {
                    script.AppendLine(command.Script);
                }
                else if (command.Mapper.Properties.Count > 0)
                {
                    is_body = true;
                }
            }

            return script.ToString();
        }
        private string CreateEntireScript()
        {
            StringBuilder script = new();

            for (int i = 0; i < ScriptCommands.Count; i++)
            {
                ScriptCommand command = ScriptCommands[i];

                if (string.IsNullOrEmpty(command.Script))
                {
                    continue;
                }

                script.AppendLine(command.Script);
            }

            return script.ToString();
        }
        private List<ScriptCommand> GetReaders()
        {
            List<ScriptCommand> mappers = new();

            foreach (ScriptCommand command in ScriptCommands)
            {
                if (command.Mapper.Properties.Count > 0)
                {
                    mappers.Add(command);
                }
            }

            return mappers;
        }
        private void Stream(in NpgsqlCommand command)
        {
            command.CommandText = CreateEntireScript();
            EntityMap mapper = GetReaders()[0].Mapper;
            DataRecord record = new(); // create buffer

            using (IDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    mapper.Map(in reader, in record); _next?.Process(record);
                }
                reader.Close();
            }
            record.Clear(); // clear buffer
        }
        private void Throttle(in NpgsqlCommand command)
        {
            int next = 0;
            List<ScriptCommand> mappers = GetReaders();
            command.CommandText = CreateHeadScript();
            EntityMap mapper = mappers[next++].Mapper;
            List<DataRecord> table = new();

            using (IDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    DataRecord record = new();
                    mapper.Map(in reader, in record);
                    table.Add(record);
                }
                reader.Close();
            }

            command.CommandText = CreateBodyScript();

            foreach (DataRecord record in table)
            {
                for (int i = 0; i < record.FieldCount; i++)
                {
                    string key = record.GetName(i);
                    object value = record.GetValue(i);

                    if (command.Parameters.Contains(key))
                    {
                        // TODO: format parameter values, see ScriptExecutor.ConfigureParameters

                        if (value is Entity entity)
                        {
                            command.Parameters[key].Value = entity.Identity.ToByteArray();
                        }
                        else
                        {
                            command.Parameters[key].Value = (value is null ? DBNull.Value : value);
                        }
                    }
                }

                next = 1;

                using (IDataReader reader = command.ExecuteReader())
                {
                    do
                    {
                        List<IDataRecord> list = new();
                        ScriptCommand sc = mappers[next++];

                        while (reader.Read())
                        {
                            DataRecord item = new();
                            sc.Mapper.Map(in reader, in item);
                            list.Add(item);
                        }

                        record.SetValue(sc.Name, list);
                    }
                    while (reader.NextResult());

                    reader.Close();
                }

                _next?.Process(record);
            }
        }
    }
}