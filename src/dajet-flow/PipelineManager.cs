using DaJet.Data;
using DaJet.Model;
using Microsoft.Extensions.Logging;
using System.Collections;

namespace DaJet.Flow
{
    public interface IPipelineManager : IDisposable
    {
        void ActivatePipelines(CancellationToken token);
        Task DeletePipeline(Guid uuid);
        void ExecutePipeline(Guid uuid);
        void DisposePipeline(Guid uuid);
        Task ReStartPipeline(Guid uuid);
        void ValidatePipeline(Guid uuid);
        PipelineInfo GetPipelineInfo(Guid uuid);
        List<PipelineInfo> GetMonitorInfo();
        List<OptionItem> GetAvailableOptions(Type owner);
        List<PipelineBlock> GetAvailableHandlers();
        void UpdatePipelineStatus(Guid uuid, in string status);
        void UpdatePipelineStartTime(Guid uuid, DateTime value);
        void UpdatePipelineFinishTime(Guid uuid, DateTime value);
        void UpdatePipelineState(Guid uuid, in PipelineState state);
        void RegisterProgressReporter(Guid uuid, in IProgress<bool> progress);
        bool TryGetProgressReporter(Guid uuid, out IProgress<bool> progress);
        void RemoveProgressReporter(Guid uuid);
    }
    public sealed class PipelineManager : IPipelineManager
    {
        private CancellationToken _token;
        private readonly ILogger _logger;
        private readonly IDataSource _source;
        private readonly IDomainModel _domain;
        private readonly IPipelineFactory _factory;
        private readonly Dictionary<Guid, IPipeline> _pipelines = new();
        private readonly Dictionary<Guid, PipelineInfo> _monitor = new();
        private readonly Dictionary<Guid, IProgress<bool>> _progress = new();
        public PipelineManager(IDomainModel domain, IDataSource source, IPipelineFactory factory, ILogger<PipelineManager> logger)
        {
            _logger = logger;
            _domain = domain ?? throw new ArgumentNullException(nameof(domain));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }
        public void ActivatePipelines(CancellationToken token)
        {
            _token = token;

            int typeCode = _domain.GetTypeCode(typeof(PipelineRecord));

            IEnumerable list = _source.Select(typeCode);

            if (list is not List<PipelineRecord> pipelines)
            {
                return;
            }

            foreach (PipelineRecord pipeline in pipelines)
            {
                if (_pipelines.ContainsKey(pipeline.Identity))
                {
                    continue;
                }

                if (!TryCreatePipeline(in pipeline, out string error))
                {
                    _logger?.LogInformation(error);

                    // broken pipeline - add info to monitor only

                    if (_monitor.TryGetValue(pipeline.Identity, out PipelineInfo current))
                    {
                        current.State = PipelineState.None;
                        current.Start = DateTime.Now;
                        current.Finish = current.Start;
                        current.Status = error;
                    }
                    else
                    {
                        DateTime now = DateTime.Now;

                        PipelineInfo info = new()
                        {
                            Uuid = pipeline.Identity,
                            Name = pipeline.Name,
                            State = PipelineState.None,
                            Activation = pipeline.Activation,
                            Start = now,
                            Finish = now,
                            Status = error
                        };

                        _monitor.Add(info.Uuid, info);
                    }
                }
            }
        }
        private bool TryCreatePipeline(in PipelineRecord pipeline, out string message)
        {
            message = string.Empty;

            try
            {
                CreatePipeline(in pipeline);
            }
            catch (Exception error)
            {
                message = ExceptionHelper.GetErrorMessageAndStackTrace(error);
            }

            return string.IsNullOrEmpty(message);
        }
        private void CreatePipeline(in PipelineRecord options)
        {
            IPipeline pipeline = _factory.Create(in options);

            if (pipeline is not null)
            {
                _pipelines.Add(pipeline.Uuid, pipeline);

                PipelineInfo info = new()
                {
                    Name = options.Name,
                    Uuid = options.Identity,
                    Activation = options.Activation
                };

                _ = _monitor.TryAdd(pipeline.Uuid, info);
                
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
            Entity pipeline = _domain.GetEntity<PipelineRecord>(uuid);

            _source.Delete(pipeline); // delete from database

            await RemovePipeline(uuid);
        }
        public async Task ReStartPipeline(Guid uuid)
        {
            await RemovePipeline(uuid);
        }
        private async Task RemovePipeline(Guid uuid)
        {
            // synchronize in-memory cache and monitor
            _ = _monitor.Remove(uuid);
            _ = _pipelines.Remove(uuid);

            if (_pipelines.TryGetValue(uuid, out IPipeline pipeline))
            {
                await pipeline.DisposeAsync();
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

        #region "PIPELINE MONITOR"
        public List<PipelineInfo> GetMonitorInfo()
        {
            return _monitor.Values.ToList();
        }
        public PipelineInfo GetPipelineInfo(Guid uuid)
        {
            if (_monitor.TryGetValue(uuid, out PipelineInfo info))
            {
                return info;
            }
            return null;
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

        public void RegisterProgressReporter(Guid uuid, in IProgress<bool> progress)
        {
            if (!_progress.TryAdd(uuid, progress))
            {
                _progress[uuid] = progress;
            }
        }
        public bool TryGetProgressReporter(Guid uuid, out IProgress<bool> progress)
        {
            return _progress.TryGetValue(uuid, out progress);
        }
        public void RemoveProgressReporter(Guid uuid)
        {
            _ = _progress.Remove(uuid);
        }

        public void ValidatePipeline(Guid uuid)
        {
            Entity entity = _domain.GetEntity<PipelineRecord>(uuid);

            if (_source.Select(entity) is PipelineRecord pipeline)
            {
                _ = _factory.Create(in pipeline);
            }
        }
        public List<PipelineBlock> GetAvailableHandlers()
        {
            List<PipelineBlock> blocks = new();

            foreach (Type type in _factory.GetRegisteredHandlers())
            {
                blocks.Add(new PipelineBlock()
                {
                    Handler = type.ToString()
                });
            }

            return blocks;
        }
        public List<OptionItem> GetAvailableOptions(Type owner)
        {
            //        List<OptionItem> options = new();

            //        foreach (PropertyInfo property in ownerType.GetProperties())
            //        {
            //            if (property.GetCustomAttribute<OptionAttribute>() is not null)
            //            {
            //                options.Add(new OptionItem()
            //                {
            //                    Name = property.Name,
            //                    Type = property.PropertyType.ToString()
            //                });
            //            }
            //        }

            //        return options;

            return new List<OptionItem>();
        }
    }
}