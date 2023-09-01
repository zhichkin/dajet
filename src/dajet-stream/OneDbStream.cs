using DaJet.Data;
using DaJet.Flow;
using DaJet.Flow.Model;
using DaJet.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;
using static Npgsql.Replication.PgOutput.Messages.RelationMessage;

namespace DaJet.Stream.PostgreSql
{
    [PipelineBlock] public sealed class OneDbStream : SourceBlock<string>
    {
        private bool _disposed = true;
        private CancellationTokenSource _cts;
        private readonly ILogger _logger;
        private readonly IPipelineManager _manager;
        private readonly InfoBaseDataMapper _databases;
        private string ConnectionString { get; set; }
        [Option] public Guid Pipeline { get; set; } = Guid.Empty;
        [Option] public string Source { get; set; } = string.Empty;
        [Option] public string PublicationName { get; set; } = string.Empty;
        [Option] public string ReplicationSlot { get; set; } = string.Empty;
        [ActivatorUtilitiesConstructor]
        public OneDbStream(IPipelineManager manager, InfoBaseDataMapper databases, ILogger<OneDbStream> logger)
        {
            _logger = logger;
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _databases = databases ?? throw new ArgumentNullException(nameof(databases));
        }
        protected override void _Configure()
        {
            InfoBaseRecord database = _databases.Select(Source) ?? throw new Exception($"Source not found: {Source}");

            ConnectionString = database.ConnectionString;
        }
        protected override void _Dispose()
        {
            if (_disposed) { return; }

            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _next?.Dispose();
            }
            finally
            {
                _disposed = true;
            }
        }
        public override void Execute()
        {
            if (!_disposed) { return; }

            _disposed = false;

            _cts = new CancellationTokenSource();

            try
            {
                Task task = ProduceDataStream();
                task.Wait(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                // do nothing
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
        private async Task ProduceDataStream()
        {
            await using (LogicalReplicationConnection connection = new(ConnectionString))
            {
                await connection.Open(_cts.Token);

                PgOutputReplicationSlot slot = new(ReplicationSlot);
                PgOutputReplicationOptions options = new(PublicationName, 1);

                await foreach (PgOutputReplicationMessage message in connection.StartReplication(slot, options, _cts.Token))
                {
                    _logger?.LogInformation($"Received message type: {message.GetType().Name}");

                    if (message is BeginMessage begin) { ProcessMessage(begin); }
                    else if (message is InsertMessage insert) { await ProcessMessage(insert); }
                    else if (message is UpdateMessage update) { await ProcessMessage(update); }
                    else if (message is DeleteMessage delete) { ProcessMessage(delete); }
                    else if (message is CommitMessage commit) { ProcessMessage(commit); }
                    else
                    {
                        // unhandled message type
                    }

                    connection.SetReplicationStatus(message.WalEnd);
                    await connection.SendStatusUpdate(_cts.Token);
                }
            }
        }
        private void ProcessMessage(BeginMessage message)
        {

        }
        private async Task ProcessMessage(InsertMessage message)
        {
            _logger?.LogInformation($"[{message.Relation.RelationName}]");
            await ProcessMessage(message.NewRow, message.Relation.Columns);
        }
        private async Task ProcessMessage(UpdateMessage message)
        {
            _logger?.LogInformation($"[{message.Relation.RelationName}]");
            await ProcessMessage(message.NewRow, message.Relation.Columns);
        }
        private void ProcessMessage(DeleteMessage message)
        {
            _logger?.LogInformation($"[{message.Relation.RelationName}] tx = {message.TransactionXid}");
        }
        private void ProcessMessage(CommitMessage message)
        {
            
        }
        private async Task ProcessMessage(ReplicationTuple tuple, IReadOnlyList<Column> columns)
        {
            int index = 0;

            await foreach (ReplicationValue value in tuple)
            {
                string columnName = columns[index].ColumnName;
                object columnValue = await value.Get();

                _logger?.LogInformation($"{columnName} = {columnValue}");

                index++;
            }
        }
    }
}