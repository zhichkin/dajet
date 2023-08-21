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
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }
        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;

            // without blocking other services, till completion DoWork procedure

            return Task.Factory.StartNew(TryDoWork, TaskCreationOptions.LongRunning);

            // NOTE: to run DoWork procedure once do the following:
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
                    Task.Delay(TimeSpan.FromSeconds(3)).Wait(_cancellationToken);
                }
                catch // (OperationCanceledException)
                {
                    // do nothing - host shutdown requested
                }
            }
        }
        private void DoWork()
        {
            _manager.ActivatePipelines(_cancellationToken);
        }
        public override void Dispose()
        {
            _manager.Dispose(); base.Dispose();

            _logger?.LogInformation("[DaJetFlowService] disposed");
        }
    }
}