using DaJet.Flow.Model;
using DaJet.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace DaJet.Flow
{
    public interface IPipeline : IDisposable
    {
        Guid Uuid { get; }
        Task Task { get; }
        string Name { get; }
        PipelineState State { get; }
        ActivationMode Activation { get; }
        Task ExecuteAsync(CancellationToken cancellationToken);
    }
    public sealed class Pipeline : Configurable, IPipeline
    {
        private Task _task;
        private int _state; // 0 == idle, 1 == starting, 2 == working, 3 == disposing
        private CancellationToken _token;
        private readonly ISourceBlock _source;
        private readonly IPipelineManager _manager;
        [Option] public int SleepTimeout { get; set; } = 60; // seconds
        [ActivatorUtilitiesConstructor] public Pipeline(PipelineOptions options, ISourceBlock source, IPipelineManager manager)
        {
            Uuid = options.Uuid;
            Name = options.Name;
            Activation = options.Activation;
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }
        public Guid Uuid { get; private set; }
        public Task Task { get { return _task; } }
        public string Name { get; private set; }
        public PipelineState State { get; private set; } = PipelineState.Stopped;
        public ActivationMode Activation { get; private set; } = ActivationMode.Manual;
        protected override void _Configure()
        {
            if (SleepTimeout < 0) { SleepTimeout = 0; } // run once
        }
        public Task ExecuteAsync(CancellationToken token)
        {
            if (IsBusy) { return _task; }

            _token = token;
            
            _task = Task.Factory.StartNew(ExecutePipeline, TaskCreationOptions.LongRunning);

            SetWorkingState();

            return _task;
        }
        private void SetIdleState() { _state = 0; }
        private void SetWorkingState() { _state = 2; }
        private bool IsBusy { get { return Interlocked.CompareExchange(ref _state, 1, 0) > 0; } }
        private bool CanDispose { get { return Interlocked.CompareExchange(ref _state, 3, 2) == 2; } }
        private bool IsStopRequested { get { return Interlocked.CompareExchange(ref _state, 2, 2) != 2; } }
        private void ExecutePipeline()
        {
            while (!_token.IsCancellationRequested)
            {
                State = PipelineState.Working;
                _manager.UpdatePipelineStatus(Uuid, string.Empty);
                _manager.UpdatePipelineStartTime(Uuid, DateTime.Now);

                try
                {
                    _source.Execute();
                }
                catch (Exception error)
                {
                    _manager.UpdatePipelineStatus(Uuid, ExceptionHelper.GetErrorMessageAndStackTrace(error));
                }

                _manager.UpdatePipelineFinishTime(Uuid, DateTime.Now);

                if (IsStopRequested || SleepTimeout == 0) { break; }

                try
                {
                    State = PipelineState.Sleeping;
                    Task.Delay(TimeSpan.FromSeconds(SleepTimeout)).Wait(_token);
                }
                catch // OperationCanceledException
                {
                    break; // host shutdown requested
                }

                if (IsStopRequested) { break; }
            }

            Dispose();
        }
        public void Dispose()
        {
            if (CanDispose)
            {
                DisposePipeline();
                SetIdleState();
            }
        }
        private void DisposePipeline()
        {
            State = PipelineState.Stopping;

            try
            {
                _source.Dispose();
            }
            catch
            {
                // TODO: log error
            }

            try
            {
                _task.Wait(_token);
            }
            catch
            {
                // OperationCanceledException
            }
            finally
            {
                if (_task.IsCompleted) { _task.Dispose(); }
            }

            State = PipelineState.Stopped;
        }
    }
}