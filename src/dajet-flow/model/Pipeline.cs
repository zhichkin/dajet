﻿using DaJet.Flow.Model;
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
        public Task Task { get { return _task; } }
        public string Name { get; private set; }
        public PipelineState State { get; private set; }
        public ActivationMode Activation { get; private set; } = ActivationMode.Manual;
        [Option] public int SleepTimeout { get; set; } = 60; // seconds
        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            if (State == PipelineState.Working || State == PipelineState.Sleeping) { return _task; }

            _token = cancellationToken;

            State = PipelineState.Working;

            _task = Task.Factory.StartNew(ExecutePipeline, TaskCreationOptions.LongRunning);

            return _task;
        }
        public void Dispose() { State = PipelineState.Stopped; }
        private void ExecutePipeline()
        {
            while (!_token.IsCancellationRequested)
            {
                try
                {
                    State = PipelineState.Working;
                    _manager.UpdatePipelineStatus(Uuid, string.Empty);
                    _manager.UpdatePipelineStartTime(Uuid, DateTime.Now);
                    
                    _source.Execute();
                }
                catch (Exception error)
                {
                    _manager.UpdatePipelineStatus(Uuid, ExceptionHelper.GetErrorMessageAndStackTrace(error));
                }

                _manager.UpdatePipelineFinishTime(Uuid, DateTime.Now);

                if (State == PipelineState.Stopped) { return; } // stopped by calling Dispose

                if (SleepTimeout <= 0) { State = PipelineState.Completed; return; } // run once

                try
                {
                    State = PipelineState.Sleeping;
                    Task.Delay(TimeSpan.FromSeconds(SleepTimeout)).Wait(_token);
                }
                catch // OperationCanceledException
                {
                    State = PipelineState.Stopped;
                }

                if (State == PipelineState.Stopped) { return; } // stopped by calling Dispose
            }
        }
    }
}