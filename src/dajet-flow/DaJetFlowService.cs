using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DaJet.Flow
{
    public sealed class DaJetFlowService : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly IPipelineManager _manager;

        private CancellationToken _cancellationToken;
        public DaJetFlowService(IPipelineManager manager, ILogger<DaJetFlowService> logger)
        {
            _logger = logger;
            _manager = manager;
        }
        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;

            // without blocking other services, till completion DoWork procedure

            return Task.Factory.StartNew(DoWork, TaskCreationOptions.LongRunning);

            // running DoWork procedure once
            // DoWork(); // blocks other services to run
            // return Task.CompletedTask;
        }
        private void DoWork()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _manager.Initialize();
                }
                catch (Exception error)
                {
                    _logger?.LogError(error.Message);
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
            base.Dispose();
            _manager.Dispose();
        }
    }
}