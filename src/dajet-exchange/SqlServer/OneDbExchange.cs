using DaJet.Flow;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Options;
using DaJet.Scripting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Diagnostics;
using System.Text;

namespace DaJet.Exchange.SqlServer
{
    [PipelineBlock] public sealed class OneDbExchange : SourceBlock<OneDbMessage>
    {
        #region "PRIVATE VARIABLES"
        private readonly IPipelineManager _manager;
        private readonly IMetadataService _metadata;
        private readonly ScriptDataMapper _scripts;
        private readonly InfoBaseDataMapper _databases;
        private readonly Dictionary<string, GeneratorResult> _commands = new();
        private bool _disposed;
        #endregion
        private string ConnectionString { get; set; }
        [Option] public Guid Pipeline { get; set; } = Guid.Empty;
        [Option] public string Source { get; set; } = string.Empty;
        [Option] public string Exchange { get; set; } = string.Empty;
        [Option] public string NodeName { get; set; } = string.Empty;
        [Option] public int BatchSize { get; set; } = 1000;
        [Option] public int Timeout { get; set; } = 10; // seconds (value of 0 indicates no limit)
        [ActivatorUtilitiesConstructor] public OneDbExchange(InfoBaseDataMapper databases, ScriptDataMapper scripts, IPipelineManager manager, IMetadataService metadata)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            _scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));
            _databases = databases ?? throw new ArgumentNullException(nameof(databases));
        }
        protected override void _Configure()
        {
            if (Timeout < 0) { Timeout = 10; }
            if (BatchSize < 1) { BatchSize = 1000; }

            InfoBaseModel database = _databases.Select(Source) ?? throw new Exception($"Source not found: {Source}");

            ConnectionString = database.ConnectionString;

            if (!_metadata.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                throw new InvalidOperationException(error);
            }

            string metadataName = "ПланОбмена." + Exchange;

            if (provider.GetMetadataObject(metadataName) is not Publication publication)
            {
                throw new InvalidOperationException($"Exchange not found: {Exchange}");
            }

            List<MetadataObject> entities = GetExchangeArticles(in publication, in provider);

            ConfigureConsumerScripts(database.Uuid, in entities, in provider);

            //GenerateConsumingScripts(in entities, in provider);
        }
        private List<MetadataObject> GetExchangeArticles(in Publication publication, in IMetadataProvider provider)
        {
            List<MetadataObject> entities = new();

            List<Guid> types = new() { MetadataTypes.Catalog, MetadataTypes.Document, MetadataTypes.InformationRegister };

            foreach (Guid article in publication.Articles.Keys)
            {
                foreach (Guid type in types)
                {
                    MetadataObject entity = provider.GetMetadataObject(type, article);

                    if (entity is not null)
                    {
                        entities.Add(entity); break;
                    }
                }
            }

            return entities;
        }
        private void ConfigureConsumerScripts(Guid database, in List<MetadataObject> entities, in IMetadataProvider provider)
        {
            foreach (MetadataObject entity in entities)
            {
                ConfigureConsumerScript(database, in provider, in entity);
            }
        }
        private void ConfigureConsumerScript(Guid database, in IMetadataProvider provider, in MetadataObject entity)
        {
            string metadataType;
            if (entity is Catalog) { metadataType = "Справочник"; }
            else if (entity is Document) { metadataType = "Документ"; }
            else if (entity is InformationRegister) { metadataType = "РегистрСведений"; }
            else
            {
                return; // unsupported metadata type - no processing ¯\_(ツ)_/¯
            }
            string metadataName = metadataType + "." + entity.Name;

            string scriptPath = $"/exchange/{Exchange}/pub/{metadataType}/{entity.Name}/consume";

            ScriptRecord record = _scripts.SelectScriptByPath(database, scriptPath);

            if (record is null)
            {
                return; // no consume script - no processing ¯\_(ツ)_/¯
            }

            if (!_scripts.TrySelect(record.Uuid, out ScriptRecord script))
            {
                throw new InvalidOperationException($"Script not found: {scriptPath}");
            }

            if (string.IsNullOrWhiteSpace(script.Script))
            {
                return; // empty script is none script ¯\_(ツ)_/¯
            }

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
        private void GenerateConsumingScripts(in List<MetadataObject> entities, in IMetadataProvider provider)
        {
            foreach (MetadataObject entity in entities)
            {
                GenerateConsumingScript(in entity, in provider);
            }
        }
        private void GenerateConsumingScript(in MetadataObject entity, in IMetadataProvider provider)
        {
            string typeName = string.Empty;
            StringBuilder script = new();

            if (entity is Catalog catalog)
            {
                typeName = "Справочник." + catalog.Name;
                script.AppendLine($"DECLARE @node string = '{NodeName}';")
                    .Append("CONSUME TOP ").Append(BatchSize).AppendLine(" Изменения.Ссылка AS Ссылка")
                    .Append("FROM Справочник.").Append(catalog.Name).AppendLine(".Изменения AS Изменения")
                    .Append("INNER JOIN ").Append(Exchange).AppendLine(" AS ПланОбмена")
                    .Append("ON Изменения.УзелОбмена = ПланОбмена.Ссылка AND ПланОбмена.Код = @node");
            }
            else if (entity is Document document)
            {
                typeName = "Документ." + document.Name;
                script.AppendLine($"DECLARE @node string = '{NodeName}';")
                    .Append("CONSUME TOP ").Append(BatchSize).AppendLine(" Изменения.Ссылка AS Ссылка")
                    .Append("FROM Документ.").Append(document.Name).AppendLine(".Изменения AS Изменения")
                    .Append("INNER JOIN ").Append(Exchange).AppendLine(" AS ПланОбмена")
                    .Append("ON Изменения.УзелОбмена = ПланОбмена.Ссылка AND ПланОбмена.Код = @node");
            }
            else if (entity is InformationRegister register)
            {
                //TODO: !!! Учесть основной отбор !!!
            }

            ScriptExecutor executor = new(provider, _metadata, _databases, _scripts);
            GeneratorResult command = executor.PrepareScript(script.ToString());

            if (command.Success)
            {
                _commands.Add(typeName, command);
            }
            else
            {
                throw new InvalidOperationException(command.Error);
            }
        }
        protected override void _Dispose()
        {
            _disposed = true;
            _next?.Dispose();
        }
        public override void Execute()
        {
            Stopwatch watch = new();
            watch.Start();

            int consumed;
            int processed = 0;
            _disposed = false;

            do
            {
                consumed = 0;

                foreach (var command in _commands)
                {
                    if (_disposed)
                    {
                        break;
                    }
                    else
                    {
                        consumed += Consume(command.Key, command.Value);
                    }
                }

                processed += consumed;
            }
            while (!_disposed && consumed > 0);

            watch.Stop();
            _manager.UpdatePipelineStatus(Pipeline, $"Processed {processed} in {watch.ElapsedMilliseconds} ms");
        }
        private int Consume(in string typeName, in GeneratorResult script)
        {
            int consumed = 0;

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

                try
                {
                    ConfigureParameters(in command);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            consumed++;

                            script.Mapper.Map(reader, out IDataRecord record);

                            OneDbMessage message = new()
                            {
                                Sender = Source,
                                Sequence = DateTime.Now.Ticks,
                                TypeName = typeName,
                                DataRecord = record
                            };

                            _next?.Process(in message);
                        }
                        reader.Close();
                    }
                    _next?.Synchronize();
                    transaction.Commit();
                }
                catch (Exception error)
                {
                    try { transaction.Rollback(); throw; }
                    catch { throw error; }
                }
            }

            return consumed;
        }
        private void ConfigureParameters(in SqlCommand command)
        {
            command.Parameters.Clear();
            command.Parameters.AddWithValue("node_name", NodeName);
            command.Parameters.AddWithValue("batch_size", BatchSize);
        }
    }
}