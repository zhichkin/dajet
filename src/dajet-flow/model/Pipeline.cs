using DaJet.Flow.Model;
using DaJet.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace DaJet.Flow
{
    public interface IPipeline : IDisposable
    {
        Guid Uuid { get; }
        string Name { get; }
        PipelineState State { get; }
        ActivationMode Activation { get; }
        Task ExecuteAsync(CancellationToken cancellationToken);
    }
    public sealed class Pipeline : Configurable, IPipeline
    {
        private Task _task;
        private CancellationToken _token;
        private readonly ISourceBlock _source;
        private readonly IPipelineManager _manager;
        [ActivatorUtilitiesConstructor] public Pipeline(PipelineOptions options, ISourceBlock source, IPipelineManager manager)
        {
            Uuid = options.Uuid;
            Name = options.Name;
            State = PipelineState.Stopped;
            Activation = options.Activation;
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }
        public Guid Uuid { get; private set; }
        public string Name { get; private set; }
        public PipelineState State { get; private set; }
        public ActivationMode Activation { get; private set; } = ActivationMode.Manual;
        [Option] public int SleepTimeout { get; set; } = 60; // seconds
        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _token = cancellationToken;

            if (State == PipelineState.Working || State == PipelineState.Sleeping) { return _task; }

            State = PipelineState.Working;

            _task = Task.Factory.StartNew(ExecutePipeline, TaskCreationOptions.LongRunning);

            return _task;
        }
        private void ExecutePipeline()
        {
            while (!_token.IsCancellationRequested)
            {
                _manager.UpdatePipelineStatus(Uuid, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                try
                {
                    State = PipelineState.Working;
                    _source.Execute();
                    _manager.UpdatePipelineStatus(Uuid, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                }
                catch (Exception error)
                {
                    string status = string.Format("[{0}] {1}",
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        ExceptionHelper.GetErrorMessageAndStackTrace(error));

                    _manager.UpdatePipelineStatus(Uuid, status);
                }

                if (State == PipelineState.Stopped) { break; } // stopped by calling Dispose

                if (SleepTimeout <= 0) { State = PipelineState.Completed; break; } // run once

                try
                {
                    State = PipelineState.Sleeping;
                    Task.Delay(TimeSpan.FromSeconds(SleepTimeout)).Wait(_token);
                }
                catch // OperationCanceledException
                {
                    State = PipelineState.Stopped;
                }

                if (State == PipelineState.Stopped) { break; } // stopped by calling Dispose
            }
        }
        public void Dispose()
        {
            State = PipelineState.Stopped;
        }
    }
}