using DaJet.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DaJet.Flow
{
    public interface IPipeline : IDisposable
    {
        bool IsRunning { get; }
        Task ExecuteAsync(CancellationToken cancellationToken);
    }
    public sealed class Pipeline : Configurable, IPipeline
    {
        private readonly Guid _uuid;
        private bool _disposed = false;
        private bool _executing = false;
        private CancellationToken _token;
        private readonly ILogger _logger;
        private readonly ISourceBlock _source;
        [ActivatorUtilitiesConstructor] public Pipeline(Guid uuid, ISourceBlock source, ILogger<Pipeline> logger)
        {
            _uuid = uuid;
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _logger = logger;
        }
        [Option] public int IdleTimeout { get; set; } = 60; // seconds
        public bool IsRunning { get { return _executing; } }
        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            if (_executing) { throw new InvalidOperationException("Already executing"); }

            _executing = true;

            _token = cancellationToken;

            return Task.Factory.StartNew(ExecutePipeline, TaskCreationOptions.LongRunning);
        }
        private void ExecutePipeline()
        {
            _logger?.LogInformation($"Pipeline {{{_uuid}}} is running ...");

            while (!_token.IsCancellationRequested && !_disposed)
            {
                try
                {
                    _source.Execute();
                }
                catch (Exception error)
                {
                    _logger?.LogError(ExceptionHelper.GetErrorMessageAndStackTrace(error));
                }

                if (IdleTimeout == 0) { break; } // run once

                try
                {
                    _logger?.LogInformation($"Pipeline {{{_uuid}}} {IdleTimeout} seconds delay.");

                    Task.Delay(TimeSpan.FromSeconds(IdleTimeout)).Wait(_token);
                }
                catch // (OperationCanceledException)
                {
                    // do nothing - the wait task has been canceled
                }
            }
        }
        public void Dispose()
        {
            if (_disposed) { return; }

            _disposed = true;
            _executing = false;

            _logger?.LogInformation($"Pipeline {{{_uuid}}} disposed");
        }
    }
}