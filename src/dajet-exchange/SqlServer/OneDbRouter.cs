using DaJet.Data;
using DaJet.Flow;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Model;
using DaJet.Scripting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Data;

namespace DaJet.Exchange.SqlServer
{
    [PipelineBlock] public sealed class OneDbRouter : BufferProcessorBlock<OneDbMessage>
    {
        #region "PRIVATE VARIABLES"
        private readonly IMetadataService _metadata;
        private readonly ScriptDataMapper _scripts;
        private readonly InfoBaseDataMapper _databases;
        private readonly Dictionary<string, GeneratorResult> _commands = new();
        private GeneratorResult _defaultRouter;
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

            InfoBaseRecord database = _databases.Select(Source) ?? throw new Exception($"Source not found: {Source}");

            ConnectionString = database.ConnectionString;

            if (!_metadata.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                throw new InvalidOperationException(error);
            }

            ConfigureDefaultRouter(database.Uuid, in provider);
            ConfigureRouterScripts(database.Uuid, in provider);
        }
        private void ConfigureDefaultRouter(Guid database, in IMetadataProvider provider)
        {
            string scriptPath = $"/exchange/{Exchange}/pub/route";

            ScriptRecord record = _scripts.SelectScriptByPath(database, scriptPath);

            if (record is null) { return; }

            if (!_scripts.TrySelect(record.Uuid, out ScriptRecord script)) { return; }

            if (string.IsNullOrWhiteSpace(script.Script)) { return; }

            ScriptExecutor executor = new(provider, _metadata, _databases, _scripts);
            GeneratorResult command = executor.PrepareScript(script.Script);

            if (!command.Success) { return; }

            _defaultRouter = command;
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
                        continue; // route script is disabled - no processing ¯\_(ツ)_/¯
                        //TODO: log exception ??? throw new InvalidOperationException($"Metadata object not found: {metadataName}");
                    }

                    List<ScriptRecord> entityScripts = _scripts.Select(entityNode);

                    bool configured = false;

                    foreach (ScriptRecord entityScript in entityScripts)
                    {
                        if (entityScript.Name == "route")
                        {
                            if (_scripts.TrySelect(entityScript.Uuid, out ScriptRecord script))
                            {
                                if (string.IsNullOrWhiteSpace(script.Script))
                                {
                                    continue; // empty script is none script ¯\_(ツ)_/¯
                                }

                                ScriptExecutor executor = new(provider, _metadata, _databases, _scripts);
                                GeneratorResult command = executor.PrepareScript(script.Script);

                                if (command.Success)
                                {
                                    configured = _commands.TryAdd(metadataName, command);
                                }
                                else
                                {
                                    throw new InvalidOperationException(command.Error);
                                }
                            }
                        }
                    }

                    if (!configured && _defaultRouter is not null)
                    {
                        _commands.Add(metadataName, _defaultRouter);
                        //TODO: cache default router values !?
                    }
                }
            }
        }
        protected override void _Process(in OneDbMessage input)
        {
            if (!_commands.TryGetValue(input.TypeName, out GeneratorResult script))
            {
                return; // messages, having no route script, are dropped
            }

            using (SqlConnection connection = new(ConnectionString))
            {
                connection.Open();

                SqlCommand command = connection.CreateCommand();
                SqlTransaction transaction = connection.BeginTransaction();

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
                    else if (value is Union union)
                    {
                        if (union.IsUndefined)
                        {
                            command.Parameters.AddWithValue(name, Guid.Empty.ToByteArray());
                        }
                        else if (union.Tag == UnionTag.Boolean)
                        {
                            command.Parameters.AddWithValue(name, union.GetBoolean() ? new byte[] { 1 } : new byte[] { 0 });
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

                try
                {
                    using (IDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            object value = script.Mapper.Properties[0].GetValue(in reader);

                            if (value is string target)
                            {
                                input.Subscribers.Add(target);
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

            if (input.Subscribers.Count > 0)
            {
                _next?.Process(in input); // continue pipeline processing
            }
            else
            {
                // Unrouted messages are dropped.
                // TODO: make an option to throw an exception !?
            }
        }
    }
}