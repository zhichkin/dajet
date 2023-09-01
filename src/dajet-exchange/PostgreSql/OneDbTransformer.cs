using DaJet.Data;
using DaJet.Flow;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Model;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Data;
using System.Text;

namespace DaJet.Exchange.PostgreSql
{
    [PipelineBlock] public sealed class OneDbTransformer : BufferProcessorBlock<OneDbMessage>
    {
        #region "PRIVATE VARIABLES"
        private readonly IMetadataService _metadata;
        private readonly ScriptDataMapper _scripts;
        private readonly InfoBaseDataMapper _databases;
        private readonly Dictionary<string, List<ScriptCommand>> _commands = new();
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

            InfoBaseRecord database = _databases.Select(Source) ?? throw new Exception($"Source not found: {Source}");

            ConnectionString = database.ConnectionString;

            if (!_metadata.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                throw new InvalidOperationException(error);
            }

            ConfigureTransformerScripts(database.Uuid, in provider);
        }
        private void ConfigureTransformerScripts(Guid database, in IMetadataProvider provider)
        {
            string pubRoot = $"/exchange/{Exchange}/pub";

            ScriptRecord pubNode = _scripts.SelectScriptByPath(database, pubRoot);

            List<ScriptRecord> typeNodes = _scripts.Select(pubNode);

            string metadataType;
            string metadataName;

            foreach (ScriptRecord typeNode in typeNodes)
            {
                metadataType = typeNode.Name;

                List<ScriptRecord> entityNodes = _scripts.Select(typeNode);

                foreach (ScriptRecord entityNode in entityNodes)
                {
                    metadataName = $"{metadataType}.{entityNode.Name}";

                    MetadataObject metadata = provider.GetMetadataObject(metadataName);

                    if (metadata is not ApplicationObject entity)
                    {
                        continue; // route script is disabled - no processing ¯\_(ツ)_/¯
                        //TODO: log exception ??? throw new InvalidOperationException($"Metadata object not found: {metadataName}");
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
                                _commands.Add(metadataName, command);
                            }
                        }
                    }
                }
            }
        }
        protected override void _Process(in OneDbMessage input)
        {
            if (!_commands.TryGetValue(input.TypeName, out List<ScriptCommand> script))
            {
                return; // messages, having no contract, are dropped
            }

            using (NpgsqlConnection connection = new(ConnectionString))
            {
                connection.Open();

                NpgsqlCommand command = connection.CreateCommand();
                NpgsqlTransaction transaction = connection.BeginTransaction();

                command.Connection = connection;
                command.Transaction = transaction;
                command.CommandText = string.Empty;
                command.CommandType = CommandType.Text;
                command.CommandTimeout = Timeout;

                ConfigureParameters(in command, in input);

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

            _next?.Process(in input); // continue pipeline processing
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
        private void ConfigureParameters(in NpgsqlCommand command, in OneDbMessage input)
        {
            if (input.DataRecord is not DataRecord record)
            {
                throw new InvalidOperationException($"DataRecord value is missing.");
            }

            for (int i = 0; i < record.FieldCount; i++)
            {
                string name = record.GetName(i);
                object value = record.GetValue(i);

                if (value is Entity entity)
                {
                    command.Parameters.AddWithValue(name, entity.Identity.ToByteArray());
                }
                else if (value is Union union)
                {
                    if (union.IsUndefined)
                    {
                        command.Parameters.AddWithValue(name, Guid.Empty.ToByteArray());
                    }
                    else if (union.Tag == UnionTag.Boolean)
                    {
                        command.Parameters.AddWithValue(name, union.GetBoolean());
                    }
                    else if (union.Tag == UnionTag.Numeric)
                    {
                        command.Parameters.AddWithValue(name, union.GetNumeric());
                    }
                    else if (union.Tag == UnionTag.DateTime)
                    {
                        command.Parameters.AddWithValue(name, union.GetDateTime());
                    }
                    else if (union.Tag == UnionTag.String)
                    {
                        command.Parameters.AddWithValue(name, union.GetString());
                    }
                    else if (union.Tag == UnionTag.Binary)
                    {
                        command.Parameters.AddWithValue(name, union.GetBinary());
                    }
                    else if (union.Tag == UnionTag.Entity)
                    {
                        command.Parameters.AddWithValue(name, union.GetEntity().Identity.ToByteArray());
                    }
                    else
                    {
                        command.Parameters.AddWithValue(name, value);
                    }
                }
                else
                {
                    command.Parameters.AddWithValue(name, value);
                }
            }
        }
        private void Stream(in NpgsqlCommand command, in List<ScriptCommand> commands, in OneDbMessage input)
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
        private void Throttle(in NpgsqlCommand command, in List<ScriptCommand> commands, in OneDbMessage input)
        {
            int next = 0;
            List<ScriptCommand> mappers = GetReaders(in commands);
            command.CommandText = CreateHeadScript(in commands);
            EntityMap mapper = mappers[next++].Mapper;

            if (input.DataRecord is not DataRecord record)
            {
                throw new InvalidOperationException($"DataRecord value is missing.");
            }
            
            using (IDataReader reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    record.Clear(); mapper.Map(in reader, in record);
                }
                reader.Close();
            }

            command.CommandText = CreateBodyScript(in commands);

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
        }
    }
}