using DaJet.Flow.Model;
using DaJet.Metadata;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DaJet.Flow
{
    public sealed class DaJetFlowService : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly IPipelineManager _manager;
        private readonly IPipelineBuilder _builder;
        private readonly Dictionary<Guid, IPipeline> _pipelines = new();
        private CancellationToken _cancellationToken;
        public DaJetFlowService(IPipelineManager manager, IPipelineBuilder builder, ILogger<DaJetFlowService> logger)
        {
            _logger = logger;
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        }
        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;

            // without blocking other services, till completion DoWork procedure

            return Task.Factory.StartNew(TryDoWork, TaskCreationOptions.LongRunning);

            // NOTE: running DoWork procedure once
            // DoWork(); // blocks other services to run
            // return Task.CompletedTask;
        }
        private void TryDoWork()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    DoWork();
                }
                catch (Exception error)
                {
                    _logger?.LogError(ExceptionHelper.GetErrorMessageAndStackTrace(error));
                }

                try
                {
                    Task.Delay(TimeSpan.FromSeconds(10)).Wait(_cancellationToken);
                }
                catch // (OperationCanceledException)
                {
                    // do nothing - the wait task has been canceled
                }
            }
        }
        public override void Dispose()
        {
            foreach (IPipeline pipeline in _pipelines.Values)
            {
                pipeline.Dispose();
            }

            _logger?.LogInformation("[DaJetFlowService] disposed");

            base.Dispose();
        }
        private void DoWork()
        {
            AssemblePipelines();
            ExecutePipelines();
        }
        private void AssemblePipelines()
        {
            List<PipelineInfo> pipelines = _manager.Select();

            foreach (PipelineInfo info in pipelines)
            {
                if (_pipelines.ContainsKey(info.Uuid))
                {
                    continue;
                }

                try
                {
                    PipelineOptions options = _manager.Select(info.Uuid);

                    IPipeline pipeline = _builder.Build(in options);

                    if (pipeline is not null)
                    {
                        _pipelines.Add(info.Uuid, pipeline);

                        _logger?.LogInformation($"Pipeline [{info.Name}] created successfully.");
                    }
                }
                catch (Exception error)
                {
                    _logger?.LogError(ExceptionHelper.GetErrorMessageAndStackTrace(error));
                }
            }
        }
        private void ExecutePipelines()
        {
            foreach (var item in _pipelines)
            {
                IPipeline pipeline = item.Value;

                if (pipeline.IsRunning) { continue; }

                Task task = pipeline.ExecuteAsync(_cancellationToken);

                _logger?.LogInformation($"Pipeline [{item.Key}] is executing ...");

                if (task.Status == TaskStatus.RanToCompletion)
                {
                    //TODO
                }
            }
        }
    }
}