using DaJet.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DaJet.Flow
{
    public interface IPipeline : IConfigurable, IDisposable
    {
        bool IsRunning { get; }
        Task ExecuteAsync(CancellationToken cancellationToken);
    }
    public sealed class Pipeline : IPipeline
    {
        private readonly Guid _uuid;
        private bool _disposed = false;
        private bool _executing = false;
        private int _idle_timeout = 60; // seconds
        private CancellationToken _token;
        private readonly ILogger _logger;
        private readonly ISourceBlock _source;
        [ActivatorUtilitiesConstructor] public Pipeline(Guid uuid, ISourceBlock source, ILogger<Pipeline> logger)
        {
            _uuid = uuid;
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _logger = logger;
        }
        public bool IsRunning { get { return _executing; } }
        public void Configure(in Dictionary<string, string> options)
        {
            if (options is null) { return; }

            if (options.TryGetValue("IdleTimeout", out string timeout) && !string.IsNullOrWhiteSpace(timeout))
            {
                if (int.TryParse(timeout, out int result) && result > 0)
                {
                    _idle_timeout = result;
                }
            }
        }
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

                if (_idle_timeout == 0) { break; } // run once

                try
                {
                    _logger?.LogInformation($"Pipeline {{{_uuid}}} {_idle_timeout} seconds delay.");

                    Task.Delay(TimeSpan.FromSeconds(_idle_timeout)).Wait(_token);
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