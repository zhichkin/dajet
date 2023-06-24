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

namespace DaJet.Stream.SqlServer
{
    [PipelineBlock] public sealed class OneDbExchange : SourceBlock<OneDbMessage>
    {
        #region "PRIVATE VARIABLES"
        private readonly IPipelineManager _manager;
        private readonly IMetadataService _metadata;
        private readonly ScriptDataMapper _scripts;
        private readonly InfoBaseDataMapper _databases;
        private readonly Dictionary<int, GeneratorResult> _commands = new();
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

            if (provider.GetMetadataObject(Exchange) is not Publication publication)
            {
                throw new InvalidOperationException($"Exchange not found: {Exchange}");
            }

            List<MetadataObject> entities = GetExchangeArticles(in publication, in provider);

            GenerateConsumingScripts(in entities, in provider);
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
        private void GenerateConsumingScripts(in List<MetadataObject> entities, in IMetadataProvider provider)
        {
            foreach (MetadataObject entity in entities)
            {
                GenerateConsumingScript(in entity, in provider);
            }
        }
        private void GenerateConsumingScript(in MetadataObject entity, in IMetadataProvider provider)
        {
            int code = 0;
            StringBuilder script = new();

            if (entity is Catalog catalog)
            {
                code = catalog.TypeCode;
                script.AppendLine("DECLARE @node string = 'DaJet';")
                    .Append("CONSUME TOP ").Append(BatchSize).AppendLine(" Изменения.Ссылка AS Ссылка")
                    .Append("FROM Справочник.").Append(catalog.Name).AppendLine(".Изменения AS Изменения")
                    .Append("INNER JOIN ").Append(Exchange).AppendLine(" AS ПланОбмена")
                    .Append("ON Изменения.УзелОбмена = ПланОбмена.Ссылка AND ПланОбмена.Код = @node");
            }
            else if (entity is Document document)
            {
                code = document.TypeCode;
                script.AppendLine("DECLARE @node string = 'DaJet';")
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
                _commands.Add(code, command);
            }
            else
            {
                throw new InvalidOperationException(command.Error);
            }
        }
        
        public override void Execute()
        {
            Stopwatch watch = new();

            watch.Start();

            int consumed;
            int processed = 0;

            do
            {
                consumed = 0;

                foreach (var command in _commands)
                {
                    consumed += Consume(command.Key, command.Value);
                }

                processed += consumed;
            }
            while (consumed > 0); //TODO: check for dispose command !!!

            watch.Stop();

            _manager.UpdatePipelineStatus(Pipeline, $"Processed {processed} in {watch.ElapsedMilliseconds} ms");
        }
        private int Consume(int typeCode, in GeneratorResult script)
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
                command.Parameters.AddWithValue("node", NodeName);

                try
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Process(reader, typeCode, script.Mapper); consumed++;
                        }
                        reader.Close();
                    }

                    WaitForCompletion();

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
        private void Process(in SqlDataReader reader, int typeCode, in EntityMap mapper)
        {
            mapper.Map(reader, out IDataRecord record);

            OneDbMessage message = new()
            {
                Sequence = 1234,
                TypeCode = typeCode,
                DataRecord = record
            };

            _next?.Process(in message);
        }
        private void WaitForCompletion()
        {
            //TODO: wait for batch have been processed by pipeline ...
        }
    }
}