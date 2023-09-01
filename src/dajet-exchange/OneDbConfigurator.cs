using DaJet.Data;
using DaJet.Flow;
using DaJet.Metadata;
using DaJet.Model;
using Microsoft.Extensions.DependencyInjection;

namespace DaJet.Exchange
{
    [PipelineBlock] public sealed class OneDbConfigurator : SourceBlock<string>
    {
        private readonly IPipelineManager _manager;
        private readonly IMetadataService _metadata;
        private readonly InfoBaseDataMapper _databases;
        [ActivatorUtilitiesConstructor] public OneDbConfigurator(InfoBaseDataMapper databases, IPipelineManager manager, IMetadataService metadata)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            _databases = databases ?? throw new ArgumentNullException(nameof(databases));
        }
        [Option] public Guid Pipeline { get; set; } = Guid.Empty;
        [Option] public bool Enabled { get; set; } = false;
        [Option] public string Database { get; set; } = string.Empty;
        [Option] public string Exchange { get; set; } = string.Empty;
        [Option] public int Timeout { get; set; } = 60; // seconds (value of 0 indicates no limit)
        protected override void _Configure()
        {
            if (Timeout < 0) { Timeout = 60; }
        }
        protected override void _Dispose()
        {
            // clear buffers
        }
        public override void Execute()
        {
            InfoBaseRecord database = _databases.Select(Database) ?? throw new InvalidOperationException($"Database is not found: {Database}");

            if (string.IsNullOrWhiteSpace(database.ConnectionString))
            {
                throw new InvalidOperationException($"Connection string is not defined: {Database}");
            }

            if (Enabled)
            {
                _manager.UpdatePipelineStartTime(Pipeline, DateTime.Now);
                _manager.UpdatePipelineStatus(Pipeline, $"Включение тюнинга [{Database}] ...");
                
                EnableChangeTracking(in database);

                _manager.UpdatePipelineFinishTime(Pipeline, DateTime.Now);
                _manager.UpdatePipelineStatus(Pipeline, $"Тюнинг [{Database}] включён.");
            }
            else
            {
                _manager.UpdatePipelineStartTime(Pipeline, DateTime.Now);
                _manager.UpdatePipelineStatus(Pipeline, $"Выключение тюнинга [{Database}] ...");

                DisableChangeTracking(in database);

                _manager.UpdatePipelineFinishTime(Pipeline, DateTime.Now);
                _manager.UpdatePipelineStatus(Pipeline, $"Тюнинг [{Database}] выключён.");
            }
        }
        private void EnableChangeTracking(in InfoBaseRecord database)
        {
            IOneDbConfigurator configurator;

            if (database.ConnectionString.StartsWith("Host"))
            {
                configurator = new PostgreSql.OneDbConfigurator();
            }
            else
            {
                configurator = new SqlServer.OneDbConfigurator();
            }

            if (!configurator.TryConfigure(in _metadata, in database, out Dictionary<string, string> log))
            {
                throw new Exception($"{log.Keys.First()} : {log.Values.First()}");
            }
        }
        private void DisableChangeTracking(in InfoBaseRecord database)
        {
            IOneDbConfigurator configurator;

            if (database.ConnectionString.StartsWith("Host"))
            {
                configurator = new PostgreSql.OneDbConfigurator();
            }
            else
            {
                configurator = new SqlServer.OneDbConfigurator();
            }

            if (!configurator.TryUninstall(in _metadata, in database, out Dictionary<string, string> log))
            {
                throw new Exception($"{log.Keys.First()} : {log.Values.First()}");
            }
        }
    }
}