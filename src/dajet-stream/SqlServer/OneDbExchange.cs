using DaJet.Flow;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Options;
using DaJet.Scripting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Diagnostics;
using System.Text;

namespace DaJet.Stream.SqlServer
{
    [PipelineBlock] public sealed class OneDbExchange : SourceBlock<OneDbMessage>
    {
        #region "PRIVATE VARIABLES"
        private ILogger _logger;
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
        [Option] public int MaxDop { get; set; } = 1;
        [Option] public int BatchSize { get; set; } = 1000;
        [Option] public int Timeout { get; set; } = 10; // seconds (value of 0 indicates no limit)
        [ActivatorUtilitiesConstructor] public OneDbExchange(InfoBaseDataMapper databases, ScriptDataMapper scripts, IPipelineManager manager, IMetadataService metadata, ILogger<OneDbExchange> logger)
        {
            _logger = logger;
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
                    Task<int>[] tasks = new Task<int>[MaxDop];
                    AsyncConsumer[] consumers = new AsyncConsumer[MaxDop];

                    for (int i = 0; i < MaxDop; i++)
                    {
                        AsyncConsumer consumer = new(_manager, command.Key, command.Value)
                        {
                            Next = _next,
                            Pipeline = Pipeline,
                            ConnectionString = ConnectionString,
                            NodeName = NodeName,
                            Timeout = Timeout
                        };
                        consumers[i] = consumer;

                        tasks[i] = consumer.Consume();
                    }

                    Task.WaitAll(tasks);

                    foreach (Task<int> task in tasks)
                    {
                        consumed += task.Result;
                    }

                    long writerCount = 0L;
                    long readerCount = 0L;
                    foreach (AsyncConsumer consumer in consumers)
                    {
                        writerCount += consumer.WriterCount;
                        readerCount += consumer.ReaderCount;
                    }
                    _logger?.LogInformation($"Writer = {writerCount} ms");
                    _logger?.LogInformation($"Reader = {readerCount} ms");
                }

                processed += consumed;
            }
            while (consumed > 0); //TODO: check for dispose command !!!

            watch.Stop();

            _manager.UpdatePipelineStatus(Pipeline, $"Processed {processed} in {watch.ElapsedMilliseconds} ms");
        }
        private int Consume(int typeCode, in GeneratorResult script)
        {
            Guid session = Guid.NewGuid(); // transaction identifier

            int sequence;

            using (ProgressTracker tracker = new())
            {
                _manager.RegisterProgressReporter(session, tracker);

                sequence = 0;

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
                        string report = string.Empty;
                        Stopwatch watch = new();
                        watch.Start();

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                sequence++; tracker.Track();

                                Process(reader, session, sequence, typeCode, script.Mapper);
                            }
                            reader.Close();

                            Process(null, session, -1, 0, null);
                        }
                        watch.Stop();
                        if (sequence > 0)
                        {
                            report = $"Writer {sequence} = {watch.ElapsedMilliseconds} ms ";
                        }

                        watch.Restart();

                        _next?.Synchronize();
                        tracker.Synchronize();
                        
                        watch.Stop();
                        if (sequence > 0)
                        {
                            report += $"Reader {sequence} = {watch.ElapsedMilliseconds} ms";
                            
                            _manager.UpdatePipelineStatus(Pipeline, report);
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

            _manager.RemoveProgressReporter(session);

            return sequence;
        }
        private void Process(in SqlDataReader reader, Guid session, int sequence, int typeCode, in EntityMap mapper)
        {
            if (sequence < 0)
            {
                OneDbMessage message = new()
                {
                    Session = session,
                    Sequence = sequence
                };

                _next?.Process(in message);
            }
            else
            {
                mapper.Map(reader, out IDataRecord record);

                OneDbMessage message = new()
                {
                    Session = session,
                    Sequence = sequence,
                    TypeCode = typeCode,
                    DataRecord = record
                };

                _next?.Process(in message);
            }
        }
    }
    internal class AsyncConsumer
    {
        private readonly int _typeCode;
        private readonly GeneratorResult _script;
        private readonly IPipelineManager _manager;
        private long _writer = 0L;
        private long _reader = 0L;
        internal long WriterCount { get { return _writer; } }
        internal long ReaderCount { get { return _reader; } }
        internal AsyncConsumer(IPipelineManager manager, int typeCode, GeneratorResult script)
        {
            _typeCode = typeCode;
            _script = script;
            _manager = manager;
        }
        internal Guid Pipeline { get; set; } = Guid.Empty;
        internal string ConnectionString { get; set; } = string.Empty;
        internal string NodeName { get; set; } = string.Empty;
        internal IInputBlock<OneDbMessage> Next { get; set; }
        internal int Timeout { get; set; } = 10; // seconds (value of 0 indicates no limit) 
        internal Task<int> Consume()
        {
            return Task.Run(ConsumeWhileNotEmpty);
        }
        private int ConsumeWhileNotEmpty()
        {
            int result = 0;
            int consumed = 0;

            do
            {
                if (Next is AsyncProcessorBlock<OneDbMessage> processor)
                {
                    result = ConsumeSingleBatch(in processor);
                }
                consumed += result;
            }
            while (result > 0);

            return consumed;
        }
        private int ConsumeSingleBatch(in AsyncProcessorBlock<OneDbMessage> processor)
        {
            processor.BeginProcessing();

            int sequence = 0;

            using (SqlConnection connection = new(ConnectionString))
            {
                connection.Open();

                SqlCommand command = connection.CreateCommand();
                SqlTransaction transaction = connection.BeginTransaction();

                command.Connection = connection;
                command.Transaction = transaction;
                command.CommandText = _script.Script;
                command.CommandType = CommandType.Text;
                command.CommandTimeout = Timeout;
                command.Parameters.AddWithValue("node", NodeName);

                try
                {
                    string report = string.Empty;
                    Stopwatch watch = new();
                    watch.Start();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            sequence++;

                            _script.Mapper.Map(reader, out IDataRecord record);

                            OneDbMessage message = new()
                            {
                                Sequence = sequence,
                                TypeCode = _typeCode,
                                DataRecord = record
                            };

                            processor.Process(in message);
                        }
                        reader.Close();
                    }
                    watch.Stop();
                    if (sequence > 0)
                    {
                        _writer += watch.ElapsedMilliseconds;
                    }

                    watch.Restart();

                    processor.Synchronize();
                    
                    watch.Stop();

                    if (sequence > 0)
                    {
                        _reader += watch.ElapsedMilliseconds;
                    }

                    transaction.Commit();
                }
                catch (Exception error)
                {
                    try { transaction.Rollback(); throw; }
                    catch { throw error; }
                }
            }

            return sequence;
        }
    }
}