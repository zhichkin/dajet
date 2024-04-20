using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DaJet.Stream
{
    internal sealed class DaJetStreamService : BackgroundService
    {
        private readonly HostConfig _config;
        private CancellationToken _cancellationToken;
        public DaJetStreamService(IOptions<HostConfig> config)
        {
            ArgumentNullException.ThrowIfNull(config);

            _config = config.Value ?? throw new ArgumentNullException(nameof(config));
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
                    FileLogger.Default.Write(ExceptionHelper.GetErrorMessageAndStackTrace(error));
                }

                try
                {
                    Task.Delay(TimeSpan.FromSeconds(_config.Refresh)).Wait(_cancellationToken);
                }
                catch // (OperationCanceledException)
                {
                    // do nothing - host shutdown requested
                }
            }
        }
        private void DoWork()
        {
            StreamManager.Activate(_config.RootPath);
        }
        public override void Dispose()
        {
            try
            {
                StreamManager.Dispose();
            }
            catch (Exception error)
            {
                FileLogger.Default.Write(ExceptionHelper.GetErrorMessageAndStackTrace(error));
            }
            finally
            {
                base.Dispose();
            }
        }
    }
}