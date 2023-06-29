using DaJet.Data;
using DaJet.Flow;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Options;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Text;

namespace DaJet.Stream.SqlServer
{
    [PipelineBlock] public sealed class OneDbTransformer : BufferProcessorBlock<OneDbMessage>
    {
        #region "PRIVATE VARIABLES"
        private readonly IMetadataService _metadata;
        private readonly ScriptDataMapper _scripts;
        private readonly InfoBaseDataMapper _databases;
        private readonly Dictionary<int, List<ScriptCommand>> _commands = new();
        #endregion
        private string ConnectionString { get; set; }
        [Option] public string Source { get; set; } = string.Empty;
        [Option] public string Exchange { get; set; } = string.Empty;
        [Option] public int Timeout { get; set; } = 10; // seconds (value of 0 indicates no limit)
        [ActivatorUtilitiesConstructor] public OneDbTransformer(InfoBaseDataMapper databases, ScriptDataMapper scripts, IMetadataService metadata)
        {
            _scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            _databases = databases ?? throw new ArgumentNullException(nameof(databases));
        }
        protected override void _Configure()
        {
            if (Timeout < 0) { Timeout = 10; }
            if (MaxDop < 1) { MaxDop = Environment.ProcessorCount; }

            InfoBaseModel database = _databases.Select(Source) ?? throw new Exception($"Source not found: {Source}");

            ConnectionString = database.ConnectionString;

            if (!_metadata.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                throw new InvalidOperationException(error);
            }

            ConfigureTransformerScripts(database.Uuid, in provider);
        }
        private void ConfigureTransformerScripts(Guid database, in IMetadataProvider provider)
        {
            string[] identifiers = Exchange.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            string exchangeName = identifiers[1];

            string pubRoot = $"/exchange/{exchangeName}/pub";

            ScriptRecord pubNode = _scripts.SelectScriptByPath(database, pubRoot);

            List<ScriptRecord> typeNodes = _scripts.Select(pubNode);

            string metadataName = string.Empty;

            foreach (ScriptRecord typeNode in typeNodes)
            {
                metadataName = typeNode.Name;

                List<ScriptRecord> entityNodes = _scripts.Select(typeNode);

                foreach (ScriptRecord entityNode in entityNodes)
                {
                    metadataName += $".{entityNode.Name}";

                    MetadataObject metadata = provider.GetMetadataObject(metadataName);

                    if (metadata is not ApplicationObject entity)
                    {
                        throw new InvalidOperationException($"Metadata object not found: {metadataName}");
                    }

                    List<ScriptRecord> entityScripts = _scripts.Select(entityNode);

                    foreach (ScriptRecord entityScript in entityScripts)
                    {
                        if (entityScript.Name == "contract")
                        {
                            if (_scripts.TrySelect(entityScript.Uuid, out ScriptRecord script))
                            {
                                ScriptExecutor executor = new(provider, _metadata, _databases, _scripts);
                                executor.PrepareScript(script.Script, out ScriptModel _, out List<ScriptCommand> command);
                                _commands.Add(entity.TypeCode, command);
                            }
                        }
                    }
                }
            }
        }
        protected override void _Process(in OneDbMessage input)
        {
            if (!_commands.TryGetValue(input.TypeCode, out List<ScriptCommand> script))
            {
                throw new InvalidOperationException();
            }

            using (SqlConnection connection = new(ConnectionString))
            {
                connection.Open();

                SqlCommand command = connection.CreateCommand();
                SqlTransaction transaction = connection.BeginTransaction();

                command.Connection = connection;
                command.Transaction = transaction;
                command.CommandText = string.Empty;
                command.CommandType = CommandType.Text;
                command.CommandTimeout = Timeout;

                for (int i = 0; i < input.DataRecord.FieldCount; i++)
                {
                    string name = input.DataRecord.GetName(i);
                    object value = input.DataRecord.GetValue(i);

                    if (value is Entity entity)
                    {
                        command.Parameters.AddWithValue(name, entity.Identity.ToByteArray());
                    }
                    else
                    {
                        command.Parameters.AddWithValue(name, value);
                    }
                }

                try
                {
                    if (IsSingleReader(in script))
                    {
                        Stream(in command, in script, in input);
                    }
                    else
                    {
                        Throttle(in command, in script, in input);
                    }

                    transaction.Commit();
                }
                catch (Exception error)
                {
                    try { transaction.Rollback(); throw; }
                    catch { throw error; }
                }
            }
        }
        private bool IsSingleReader(in List<ScriptCommand> script)
        {
            int count = 0;

            foreach (ScriptCommand command in script)
            {
                if (command.Mapper.Properties.Count > 0)
                {
                    count++;
                }
            }

            return (count == 1);
        }
        private string CreateHeadScript(in List<ScriptCommand> commands)
        {
            StringBuilder script = new();

            for (int i = 0; i < commands.Count; i++)
            {
                ScriptCommand command = commands[i];

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
        private string CreateBodyScript(in List<ScriptCommand> commands)
        {
            StringBuilder script = new();

            bool is_body = false;

            for (int i = 0; i < commands.Count; i++)
            {
                ScriptCommand command = commands[i];

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
        private string CreateEntireScript(in List<ScriptCommand> commands)
        {
            StringBuilder script = new();

            for (int i = 0; i < commands.Count; i++)
            {
                ScriptCommand command = commands[i];

                if (string.IsNullOrEmpty(command.Script))
                {
                    continue;
                }

                script.AppendLine(command.Script);
            }

            return script.ToString();
        }
        private List<ScriptCommand> GetReaders(in List<ScriptCommand> commands)
        {
            List<ScriptCommand> mappers = new();

            foreach (ScriptCommand command in commands)
            {
                if (command.Mapper.Properties.Count > 0)
                {
                    mappers.Add(command);
                }
            }

            return mappers;
        }
        private void Stream(in SqlCommand command, in List<ScriptCommand> commands, in OneDbMessage input)
        {
            command.CommandText = CreateEntireScript(in commands);
            EntityMap mapper = GetReaders(in commands)[0].Mapper;

            using (IDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (input.DataRecord is DataRecord record)
                    {
                        record.Clear(); mapper.Map(in reader, in record);
                    }
                }
                reader.Close();
            }
        }
        private void Throttle(in SqlCommand command, in List<ScriptCommand> commands, in OneDbMessage input)
        {
            int next = 0;
            List<ScriptCommand> mappers = GetReaders(in commands);
            command.CommandText = CreateHeadScript(in commands);
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

            command.CommandText = CreateBodyScript(in commands);

            foreach (DataRecord record in table)
            {
                for (int i = 0; i < record.FieldCount; i++)
                {
                    string key = record.GetName(i);
                    object value = record.GetValue(i);

                    if (command.Parameters.Contains(key))
                    {
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

                input.DataRecord = record;
            }
        }
    }
}