using DaJet.Flow.Model;
using DaJet.Metadata;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DaJet.Flow
{
    public interface IPipelineManager : IDisposable
    {
        void ExecutePipeline(Guid uuid);
        void DisposePipeline(Guid uuid);
        List<PipelineInfo> GetMonitorInfo();
        void ActivatePipelines(CancellationToken token);
        void UpdatePipelineStatus(Guid uuid, in string status);
        void UpdatePipelineStartTime(Guid uuid, DateTime value);
        void UpdatePipelineFinishTime(Guid uuid, DateTime value);
    }
    public sealed class PipelineManager : IPipelineManager
    {
        private CancellationToken _token;
        private readonly ILogger _logger;
        private readonly IPipelineBuilder _builder;
        private readonly IPipelineOptionsProvider _options;
        private readonly Dictionary<Guid, IPipeline> _pipelines = new();
        private readonly Dictionary<Guid, PipelineInfo> _monitor = new();
        public PipelineManager(IPipelineOptionsProvider options, IPipelineBuilder builder, ILogger<PipelineManager> logger)
        {
            _logger = logger;
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        }
        public void ActivatePipelines(CancellationToken token)
        {
            _token = token;

            List<PipelineInfo> pipelines = _options.Select();

            foreach (PipelineInfo info in pipelines)
            {
                if (_pipelines.ContainsKey(info.Uuid))
                {
                    continue; // TODO: check if pipeline options has been changed and re-run it
                }
                else
                {
                    TryCreatePipeline(in info);
                }
            }
        }
        private void TryCreatePipeline(in PipelineInfo info)
        {
            try
            {
                PipelineOptions options = _options.Select(info.Uuid);

                if (options is not null)
                {
                    CreatePipeline(in options);
                }
            }
            catch (Exception error)
            {
                _logger?.LogError(ExceptionHelper.GetErrorMessageAndStackTrace(error));
            }
        }
        private void CreatePipeline(in PipelineOptions options)
        {
            IPipeline pipeline = _builder.Build(in options);

            if (pipeline is not null)
            {
                _pipelines.Add(pipeline.Uuid, pipeline);
                _monitor.Add(pipeline.Uuid, CreatePipelineInfo(in pipeline));

                _logger?.LogInformation($"Pipeline [{pipeline.Name}] created successfully.");

                if (pipeline.Activation == ActivationMode.Auto)
                {
                    Task task = pipeline.ExecuteAsync(_token);
                }
            }
        }
        private PipelineInfo CreatePipelineInfo(in IPipeline pipeline)
        {
            return new PipelineInfo()
            {
                Uuid = pipeline.Uuid,
                Name = pipeline.Name,
                State = pipeline.State,
                Status = string.Empty,
                Activation = pipeline.Activation
            };
        }
        public List<PipelineInfo> GetMonitorInfo()
        {
            List<PipelineInfo> monitor = new();

            foreach (PipelineInfo info in _monitor.Values)
            {
                if (_pipelines.TryGetValue(info.Uuid, out IPipeline pipeline))
                {
                    info.State = pipeline.State;
                }

                monitor.Add(info);
            }

            return monitor;
        }
        public void ExecutePipeline(Guid uuid)
        {
            if (_pipelines.TryGetValue(uuid, out IPipeline pipeline))
            {
                if (pipeline.State == PipelineState.Stopped || pipeline.State == PipelineState.Completed)
                {
                    Task task = pipeline.ExecuteAsync(_token);
                }
            }
        }
        public void DisposePipeline(Guid uuid)
        {
            if (_pipelines.TryGetValue(uuid, out IPipeline pipeline))
            {
                if (pipeline.State == PipelineState.Working || pipeline.State == PipelineState.Sleeping)
                {
                    pipeline.Dispose();
                }
            }
        }
        public void Dispose()
        {
            foreach (IPipeline pipeline in _pipelines.Values)
            {
                pipeline.Dispose();
            }
            _monitor.Clear();
            _pipelines.Clear();
        }

        //

        public void UpdatePipelineStatus(Guid uuid, in string status)
        {
            if (_monitor.TryGetValue(uuid, out PipelineInfo info))
            {
                info.Status = status;
            }
        }
        public void UpdatePipelineStartTime(Guid uuid, DateTime value)
        {
            if (_monitor.TryGetValue(uuid, out PipelineInfo info))
            {
                info.Start = value;
            }
        }
        public void UpdatePipelineFinishTime(Guid uuid, DateTime value)
        {
            if (_monitor.TryGetValue(uuid, out PipelineInfo info))
            {
                info.Finish = value;
            }
        }
    }
}