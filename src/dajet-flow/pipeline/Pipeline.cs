using DaJet.Model;
using Microsoft.Extensions.DependencyInjection;

namespace DaJet.Flow
{
    public interface IPipeline : IDisposable, IAsyncDisposable
    {
        Guid Uuid { get; }
        string Name { get; }
        void Initialize(ISourceBlock handler);
        Task ExecuteAsync(CancellationToken token);

        void UpdateMonitorStatus(in string status);
        void UpdateMonitorStartTime(DateTime value);
        void UpdateMonitorFinishTime(DateTime value);
    }
    public sealed class Pipeline : IPipeline
    {
        private const string PIPELINE_CAN_BE_INITIALIZED_ONLY_ONCE =
            "Pipeline {0} is initialized already. Pipeline can be initialized only once.";

        private Task _task;
        private int _state; // 0 == idle, 1 == starting, 2 == working, 3 == disposing
        private CancellationToken _token;
        private CancellationTokenSource _cts;
        private CancellationTokenRegistration _ctr;
        private ISourceBlock _handler;
        private readonly PipelineOptions _options;
        private readonly IPipelineManager _manager;
        [ActivatorUtilitiesConstructor] public Pipeline(PipelineOptions options, IPipelineManager manager)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));

            Uuid = options.Uuid;
            Name = options.Name;
        }
        public Guid Uuid { get; private set; }
        public string Name { get; private set; }
        public void Initialize(ISourceBlock handler)
        {
            if (handler is null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (Interlocked.CompareExchange(ref _handler, handler, null) is not null)
            {
                throw new InvalidOperationException(string.Format(PIPELINE_CAN_BE_INITIALIZED_ONLY_ONCE, Uuid));
            }
        }
        private bool CanExecute { get { return Interlocked.CompareExchange(ref _state, 1, 0) == 0; } }
        private bool CanDispose { get { return Interlocked.CompareExchange(ref _state, 3, 2) == 2; } }
        private void SetIdleState() { _ = Interlocked.Exchange(ref _state, 0); }
        private void SetWorkingState() { _ = Interlocked.Exchange(ref _state, 2); }
        public Task ExecuteAsync(CancellationToken token)
        {
            if (_handler is null)
            {
                throw new InvalidOperationException($"Pipeline [{Name}] is not initialized {{{Uuid}}}");
            }

            if (CanExecute)
            {
                _cts = CancellationTokenSource.CreateLinkedTokenSource(token);

                _token = _cts.Token;

                _task = Task.Factory.StartNew(ExecutePipeline, TaskCreationOptions.LongRunning);

                SetWorkingState();
            }

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
                    _handler.Execute();
                }
                catch (Exception error)
                {
                    string errorMessage = _options.ShowStackTrace
                        ? ExceptionHelper.GetErrorMessageAndStackTrace(error)
                        : ExceptionHelper.GetErrorMessage(error);
                    _manager.UpdatePipelineStatus(Uuid, errorMessage);
                    FileLogger.Default.Write($"[{Name}]: {errorMessage}");
                }

                _manager.UpdatePipelineFinishTime(Uuid, DateTime.Now);

                if (_options.SleepTimeout == 0) { break; } // run once

                if (_token.IsCancellationRequested) { break; }
                
                try
                {
                    _manager.UpdatePipelineState(Uuid, PipelineState.Sleeping);
                    Task.Delay(TimeSpan.FromSeconds(_options.SleepTimeout)).Wait(_token);
                }
                catch // OperationCanceledException
                {
                    break; // The pipeline Dispose method is called or host shutdown requested
                }
            }

            Dispose();
        }
        private void DisposePipeline()
        {
            _manager.UpdatePipelineState(Uuid, PipelineState.Disposing);

            try
            {
                _handler.Dispose();
            }
            catch
            {
                // TODO: log error
            }

            _manager.UpdatePipelineState(Uuid, PipelineState.Idle);
        }
        private void CancellationHandler()
        {
            _ctr.Dispose(); // unregister handler from cancellation token
            _cts.Dispose(); // dispose pipeline cancellation token source
            _cts = null;
            
            DisposePipeline(); // dispose pipeline source block

            SetIdleState();
        }
        public void Dispose()
        {
            if (CanDispose)
            {
                try
                {
                    _cts.Cancel();
                }
                finally
                {
                    _ctr = _token.Register(CancellationHandler);
                }
            }
        }
        private ValueTask DisposeAsyncCore()
        {
            try
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
            catch (Exception error)
            {
                return ValueTask.FromException(error);
            }
        }
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
        }

        public void UpdateMonitorStatus(in string status)
        {
            _manager.UpdatePipelineStatus(Uuid, in status);
        }
        public void UpdateMonitorStartTime(DateTime value)
        {
            _manager.UpdatePipelineStartTime(Uuid, value);
        }
        public void UpdateMonitorFinishTime(DateTime value)
        {
            _manager.UpdatePipelineFinishTime(Uuid, value);
        }
    }
}