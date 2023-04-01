using System.Reflection;
using System.Runtime.Loader;

namespace DaJet.Flow
{
    public interface IPipelineManager : IDisposable
    {
        void Initialize();
        Type ResolveHandler(string name);
        Dictionary<string, IPipeline> Pipelines { get; }
    }
    public sealed class PipelineManager : IPipelineManager
    {
        private readonly List<Assembly> _assemblies = new();
        private readonly PipelineOptionsProvider _provider;
        private readonly CancellationTokenSource _tokenSource = new();
        public PipelineManager(PipelineOptionsProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }
        public Dictionary<string, IPipeline> Pipelines { get; } = new();
        public Type ResolveHandler(string name)
        {
            Type type;

            foreach (Assembly assembly in _assemblies)
            {
                type = assembly.GetType(name);

                if (type is not null)
                {
                    return type;
                }
            }

            throw new InvalidOperationException($"Failed to resolve handler [{name}]");
        }
        public void Dispose()
        {
            _tokenSource.Cancel();

            foreach (IPipeline pipeline in Pipelines.Values)
            {
                pipeline.Dispose();
            }
        }
        public void Initialize()
        {
            InitializeAssemblies();
            InitializePipelines();
            RunPipelines();
        }
        private void InitializeAssemblies()
        {
            foreach (string filePath in Directory.GetFiles(AppContext.BaseDirectory, "DaJet.Flow.*"))
            {
                if (Path.GetExtension(filePath) == ".dll")
                {
                    Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(filePath);

                    if (assembly is not null)
                    {
                        _assemblies.Add(assembly); //TODO: ignore duplicates !!!
                    }

                    //TODO: logger.Information($"[{assembly.FullName}] loaded successfully.");
                }
            }
        }
        private void InitializePipelines()
        {
            PipelineBuilder builder = new(this);

            List<PipelineOptions> pipelines = _provider.Select();

            foreach (PipelineOptions options in pipelines)
            {
                PipelineOptions entity = _provider.Select(options.Uuid);

                IPipeline pipeline = builder.Build(in entity);

                Pipelines.Add(options.Name, pipeline);
            }
        }
        private void RunPipelines()
        {
            foreach (IPipeline pipeline in Pipelines.Values)
            {
                pipeline.Pump(_tokenSource.Token);
            }
        }
    }
}