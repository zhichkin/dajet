using DaJet.Data;
using DaJet.Flow;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Options;
using DaJet.Scripting;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Data;

namespace DaJet.Exchange.PostgreSql
{
    [PipelineBlock] public sealed class OneDbRouter : BufferProcessorBlock<OneDbMessage>
    {
        #region "PRIVATE VARIABLES"
        private readonly IMetadataService _metadata;
        private readonly ScriptDataMapper _scripts;
        private readonly InfoBaseDataMapper _databases;
        private readonly Dictionary<string, GeneratorResult> _commands = new();
        #endregion
        private string ConnectionString { get; set; }
        [Option] public string Source { get; set; } = string.Empty;
        [Option] public string Exchange { get; set; } = string.Empty;
        [Option] public int Timeout { get; set; } = 10; // seconds (value of 0 indicates no limit)
        [ActivatorUtilitiesConstructor] public OneDbRouter(InfoBaseDataMapper databases, ScriptDataMapper scripts, IMetadataService metadata)
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
            
            ConfigureRouterScripts(database.Uuid, in provider);
        }
        private void ConfigureRouterScripts(Guid database, in IMetadataProvider provider)
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
                        throw new InvalidOperationException($"Metadata object not found: {metadataName}");
                    }

                    List<ScriptRecord> entityScripts = _scripts.Select(entityNode);

                    foreach (ScriptRecord entityScript in entityScripts)
                    {
                        if (entityScript.Name == "route")
                        {
                            if (_scripts.TrySelect(entityScript.Uuid, out ScriptRecord script))
                            {
                                ScriptExecutor executor = new(provider, _metadata, _databases, _scripts);
                                GeneratorResult command = executor.PrepareScript(script.Script);

                                if (command.Success)
                                {
                                    _commands.Add(metadataName, command);
                                }
                                else
                                {
                                    throw new InvalidOperationException(command.Error);
                                }
                            }
                        }
                    }
                }
            }
        }
        protected override void _Process(in OneDbMessage input)
        {
            if (!_commands.TryGetValue(input.TypeName, out GeneratorResult script))
            {
                throw new InvalidOperationException();
            }

            using (NpgsqlConnection connection = new(ConnectionString))
            {
                connection.Open();

                NpgsqlCommand command = connection.CreateCommand();
                NpgsqlTransaction transaction = connection.BeginTransaction();

                command.Connection = connection;
                command.Transaction = transaction;
                command.CommandText = script.Script;
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
                    using (IDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            object value = script.Mapper.Properties[0].GetValue(in reader);
                            
                            if (value is string target)
                            {
                                input.Subscribers.Add(target); //reader.GetString(0)
                            }
                        }
                        reader.Close();
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
    }
}