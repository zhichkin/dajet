using DaJet.Model;
using Microsoft.Extensions.DependencyInjection;

namespace DaJet.Flow
{
    public interface IPipeline : IDisposable, IAsyncDisposable
    {
        Guid Uuid { get; }
        string Name { get; }
        Task ExecuteAsync(CancellationToken token);
    }
    public sealed class Pipeline : IPipeline
    {
        private Task _task;
        private int _state; // 0 == idle, 1 == starting, 2 == working, 3 == disposing
        private CancellationToken _token;
        private CancellationTokenSource _cts;
        private CancellationTokenRegistration _ctr;
        private readonly PipelineOptions _options;
        private readonly ISourceBlock _source;
        private readonly IPipelineManager _manager;
        [ActivatorUtilitiesConstructor] public Pipeline(PipelineOptions options, ISourceBlock source, IPipelineManager manager)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            Uuid = options.Owner.Identity;
            Name = options.Name;
        }
        public Guid Uuid { get; private set; }
        public string Name { get; private set; }
        private bool CanExecute { get { return Interlocked.CompareExchange(ref _state, 1, 0) == 0; } }
        private bool CanDispose { get { return Interlocked.CompareExchange(ref _state, 3, 2) == 2; } }
        private void SetIdleState() { _ = Interlocked.Exchange(ref _state, 0); }
        private void SetWorkingState() { _ = Interlocked.Exchange(ref _state, 2); }
        public Task ExecuteAsync(CancellationToken token)
        {
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
                    _source.Execute();
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
                _source.Dispose();
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
    }
}