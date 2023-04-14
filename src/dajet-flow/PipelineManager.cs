using DaJet.Flow.Model;
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
        void UpdatePipelineState(Guid uuid, in PipelineState state);
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
                PipelineInfo info = new()
                {
                    Uuid = options.Uuid,
                    Name = options.Name,
                    Activation = options.Activation
                };

                _monitor.Add(pipeline.Uuid, info);
                _pipelines.Add(pipeline.Uuid, pipeline);

                _logger?.LogInformation("Pipeline [{name}] created successfully.", options.Name);

                if (options.Activation == ActivationMode.Auto)
                {
                    _ = pipeline.ExecuteAsync(_token);
                }
            }
        }

        public void ExecutePipeline(Guid uuid)
        {
            if (_pipelines.TryGetValue(uuid, out IPipeline pipeline))
            {
                _ = pipeline.ExecuteAsync(_token);
            }
        }
        public void DisposePipeline(Guid uuid)
        {
            if (_pipelines.TryGetValue(uuid, out IPipeline pipeline))
            {
                pipeline.Dispose();
            }
        }
        public async Task DeletePipeline(Guid uuid)
        {
            PipelineOptions options = _options.Select(uuid);

            if (options is not null)
            {
                _ = _options.Delete(in options); // delete from database
            }

            await RemovePipeline(uuid);
        }
        public async Task ReStartPipeline(Guid uuid)
        {
            await RemovePipeline(uuid);
        }
        private async Task RemovePipeline(Guid uuid)
        {
            if (_pipelines.TryGetValue(uuid, out IPipeline pipeline))
            {
                await pipeline.DisposeAsync();
            }

            // synchronize in-memory cache and monitor
            _ = _monitor.Remove(uuid);
            _ = _pipelines.Remove(uuid);
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

        #region "PIPELINE MONITOR"
        public List<PipelineInfo> GetMonitorInfo()
        {
            return _monitor.Values.ToList();
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
        public void UpdatePipelineState(Guid uuid, in PipelineState state)
        {
            if (_monitor.TryGetValue(uuid, out PipelineInfo info))
            {
                info.State = state;
            }
        }
        #endregion
    }
}