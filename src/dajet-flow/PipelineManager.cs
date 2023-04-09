using DaJet.Flow.Model;
using DaJet.Metadata;
using Microsoft.Extensions.Logging;

namespace DaJet.Flow
{
    public interface IPipelineManager : IDisposable
    {
        void ActivatePipelines(CancellationToken token);
        Task DeletePipeline(Guid uuid);
        void ExecutePipeline(Guid uuid);
        void DisposePipeline(Guid uuid);
        Task ReStartPipeline(Guid uuid);
        List<PipelineInfo> GetMonitorInfo();
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
                    continue;
                }

                if (!TryCreatePipeline(in info, out string error))
                {
                    // broken pipeline - add info to monitor only

                    if (_monitor.TryGetValue(info.Uuid, out PipelineInfo current))
                    {
                        current.State = PipelineState.None;
                        current.Start = DateTime.Now;
                        current.Finish = current.Start;
                        current.Status = error;
                    }
                    else
                    {
                        info.State = PipelineState.None;
                        info.Start = DateTime.Now;
                        info.Finish = info.Start;
                        info.Status = error;

                        _monitor.Add(info.Uuid, info);
                    }
                }
            }
        }
        private bool TryCreatePipeline(in PipelineInfo info, out string message)
        {
            message = string.Empty;

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
                message = ExceptionHelper.GetErrorMessageAndStackTrace(error);
            }

            return string.IsNullOrEmpty(message);
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
                    _ = pipeline.ExecuteAsync(_token);
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

        private async Task RemovePipeline(Guid uuid, bool waitForCompletion)
        {
            if (_pipelines.TryGetValue(uuid, out IPipeline pipeline))
            {
                if (pipeline.State == PipelineState.Working || pipeline.State == PipelineState.Sleeping)
                {
                    pipeline.Dispose();
                }

                if (waitForCompletion)
                {
                    try
                    {
                        if (pipeline.Task is not null)
                        {
                            await pipeline.Task?.WaitAsync(_token);
                        }
                    }
                    catch
                    {
                        // do nothing
                    }
                }
            }

            // synchronize in-memory cache and monitor
            _ = _monitor.Remove(uuid);
            _ = _pipelines.Remove(uuid);
        }
        
        public void ExecutePipeline(Guid uuid)
        {
            if (_pipelines.TryGetValue(uuid, out IPipeline pipeline))
            {
                if (pipeline.State == PipelineState.Stopped || pipeline.State == PipelineState.Completed)
                {
                    _ = pipeline.ExecuteAsync(_token);
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
                pipeline.Task?.Wait(_token);
            }
        }
        public async Task DeletePipeline(Guid uuid)
        {
            PipelineOptions options = _options.Select(uuid);

            if (options is not null)
            {
                _ = _options.Delete(in options); // delete from database
            }

            await RemovePipeline(uuid, false);
        }
        public async Task ReStartPipeline(Guid uuid)
        {
            await RemovePipeline(uuid, true);
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