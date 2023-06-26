using DaJet.Data;
using DaJet.Flow;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Options;
using DaJet.Scripting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Threading.Channels;

namespace DaJet.Stream.SqlServer
{
    [PipelineBlock] public sealed class OneDbRouter : AsyncProcessorBlock<OneDbMessage>
    {
        #region "PRIVATE VARIABLES"
        private readonly IPipelineManager _manager;
        private readonly IMetadataService _metadata;
        private readonly ScriptDataMapper _scripts;
        private readonly InfoBaseDataMapper _databases;
        private readonly Dictionary<int, GeneratorResult> _commands = new();
        private Channel<OneDbMessage> _channel;
        #endregion
        private string ConnectionString { get; set; }
        [Option] public Guid Pipeline { get; set; } = Guid.Empty;
        [Option] public string Source { get; set; } = string.Empty;
        [Option] public string Exchange { get; set; } = string.Empty;
        [Option] public int MaxDop { get; set; } = 1;
        [Option] public int Timeout { get; set; } = 10; // seconds (value of 0 indicates no limit)
        [ActivatorUtilitiesConstructor] public OneDbRouter(InfoBaseDataMapper databases, ScriptDataMapper scripts, IPipelineManager manager, IMetadataService metadata)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            _scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));
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
                        if (entityScript.Name == "routing")
                        {
                            if (_scripts.TrySelect(entityScript.Uuid, out ScriptRecord script))
                            {
                                ScriptExecutor executor = new(provider, _metadata, _databases, _scripts);
                                GeneratorResult command = executor.PrepareScript(script.Script);

                                if (command.Success)
                                {
                                    _commands.Add(entity.TypeCode, command);
                                }
                                else
                                {
                                    //TODO: throw new InvalidOperationException(command.Error);
                                }
                            }
                        }
                        else
                        {
                            //TODO: throw new InvalidOperationException($"Router script not found: {metadataName}");
                        }
                    }
                }
            }
        }

        public override void Process(in OneDbMessage input)
        {
            if (_channel is null)
            {
                _channel = Channel.CreateUnbounded<OneDbMessage>(new UnboundedChannelOptions()
                {
                    SingleWriter = false,
                    SingleReader = false,
                    AllowSynchronousContinuations = true
                });

                for (int i = 0; i < MaxDop; i++)
                {
                    _ = Task.Run(RouteMessages);
                }
            }

            _channel.Writer.WriteAsync(input);
        }
        private async ValueTask RouteMessages()
        {
            if (_channel is null)
            {
                return; // _channel must be disposed
            }

            while (await _channel.Reader.WaitToReadAsync())
            {
                if (_channel.Reader.TryRead(out OneDbMessage message))
                {
                    bool success = true;

                    try
                    {
                        RouteMessage(in message);
                    }
                    catch
                    {
                        success = false;
                    }

                    if (_manager.TryGetProgressReporter(message.Session, out IProgress<bool> progress))
                    {
                        progress?.Report(success);
                    }

                    _next?.Process(in message);
                }

                if (_channel is null)
                {
                    break; // _channel must be disposed
                }
            }
        }
        private void RouteMessage(in OneDbMessage message)
        {
            if (!_commands.TryGetValue(message.TypeCode, out GeneratorResult script))
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
                command.CommandText = script.Script;
                command.CommandType = CommandType.Text;
                command.CommandTimeout = Timeout;

                for (int i = 0; i < message.DataRecord.FieldCount; i++)
                {
                    string name = message.DataRecord.GetName(i);
                    object value = message.DataRecord.GetValue(i);

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
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            message.Subscribers.Add(reader.GetString(0));
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
        protected override void _Dispose()
        {
            if (_channel is null)
            {
                return;
            }

            if (_channel.Writer.TryComplete())
            {
                _channel.Reader.Completion.ContinueWith((task) =>
                {
                    //TODO: dispose local resources
                });
            }

            _channel = null;
        }
    }
}