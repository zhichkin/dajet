using DaJet.Flow.Model;
using Microsoft.Extensions.DependencyInjection;

namespace DaJet.Flow
{
    public interface IPipeline : IDisposable, IAsyncDisposable
    {
        Guid Uuid { get; }
        string Name { get; }
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
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }
        public Guid Uuid { get; private set; }
        public string Name { get; private set; }
        protected override void _Configure()
        {
            if (SleepTimeout < 0) { SleepTimeout = 0; } // run once
        }
        private bool IsBusy { get { return Interlocked.CompareExchange(ref _state, 1, 0) > 0; } }
        private bool CanDispose { get { return Interlocked.CompareExchange(ref _state, 3, 2) == 2; } }
        private bool IsStopRequested { get { return Interlocked.CompareExchange(ref _state, 2, 2) != 2; } }
        private void SetPipelineState(PipelineState state) { _ = Interlocked.Exchange(ref _state, (int)state); }
        public Task ExecuteAsync(CancellationToken token)
        {
            if (IsBusy) { return _task; }

            _token = token;

            _task = Task.Factory.StartNew(ExecutePipeline, TaskCreationOptions.LongRunning);

            SetPipelineState(PipelineState.Working);

            return _task;
        }
        private void ExecutePipeline()
        {
            while (!_token.IsCancellationRequested)
            {
                _manager.UpdatePipelineStatus(Uuid, string.Empty);
                _manager.UpdatePipelineStartTime(Uuid, DateTime.Now);
                _manager.UpdatePipelineState(Uuid, PipelineState.Working);

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
                    _manager.UpdatePipelineState(Uuid, PipelineState.Sleeping);
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
                SetPipelineState(PipelineState.Idle);
            }
        }
        private void DisposePipeline()
        {
            _manager.UpdatePipelineState(Uuid, PipelineState.Disposing);

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
                //TODO: _task.Wait(_token); !!!
            }
            catch
            {
                // OperationCanceledException
            }
            finally
            {
                //TODO: if (_task.IsCompleted) { _task.Dispose(); } ???
            }

            _manager.UpdatePipelineState(Uuid, PipelineState.Idle);
        }
        private ValueTask DisposeAsyncFake() { return ValueTask.CompletedTask; }
        public async ValueTask DisposeAsync() { await DisposeAsyncFake(); Dispose(); }
    }
}