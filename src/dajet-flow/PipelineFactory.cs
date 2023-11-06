using DaJet.Data;
using DaJet.Model;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace DaJet.Flow
{
    public interface IPipelineFactory
    {
        IEnumerable<Type> GetRegisteredHandlers();
        IPipeline Create(in PipelineRecord record);
    }
    public sealed class PipelineFactory : IPipelineFactory
    {
        private readonly IDataSource _dataSource;
        private readonly IDomainModel _domainModel;
        private readonly IAssemblyManager _assemblyManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly OptionsFactoryProvider _optionsFactory;
        private readonly Dictionary<Type, ServiceCreationInfo> _registry = new();
        public PipelineFactory(
            IDataSource dataSource, IDomainModel domainModel, IServiceProvider serviceProvider,
            IAssemblyManager assemblyManager, OptionsFactoryProvider optionsFactory)
        {
            _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            _domainModel = domainModel ?? throw new ArgumentNullException(nameof(domainModel));
            _assemblyManager = assemblyManager ?? throw new ArgumentNullException(nameof(assemblyManager));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _optionsFactory = optionsFactory ?? throw new ArgumentNullException(nameof(optionsFactory));

            Initialize();
        }
        private void Initialize()
        {
            foreach (Assembly assembly in _assemblyManager.Assemblies)
            {
                Initialize(assembly);
            }
        }
        private void Initialize(Assembly assembly)
        {
            if (assembly is null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            foreach (Type type in assembly.GetTypes())
            {
                RegisterPipelineBlock(type);
            }
        }
        private void RegisterPipelineBlock(Type type)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (!type.IsPipelineBlock(out Type inputType, out Type outputType))
            {
                return;
            }

            Type[] options = type.GetPipelineBlockOptions();

            ObjectFactory factory = ActivatorUtilities.CreateFactory(type, options);

            ServiceCreationInfo info = new()
            {
                Service = type,
                Options = options,
                Factory = factory,
                Input = inputType,
                Output = outputType
            };

            _ = _registry.TryAdd(type, info);
        }
        
        public IPipeline Create(in PipelineRecord record)
        {
            List<object> blocks = CreatePipelineBlocks(in record);

            if (blocks.Count == 0)
            {
                throw new InvalidOperationException($"Pipeline does not have any blocks.");
            }

            if (blocks[0] is not ISourceBlock source)
            {
                throw new InvalidOperationException($"Pipeline source type does not implement DaJet.Flow.ISourceBlock interface.");
            }

            AssemblePipeline(blocks);

            IOptionsFactory<PipelineOptions> factory = _optionsFactory.GetRequiredFactory<PipelineOptions>();

            PipelineOptions options = factory.Create(record.GetEntity());

            object[] parameters = new object[] { options, source };
            
            object instance = ActivatorUtilities.CreateInstance(_serviceProvider, typeof(Pipeline), parameters);

            if (instance is IPipeline pipeline)
            {
                return pipeline;
            }

            return null;
        }
        private void AssemblePipeline(in List<object> blocks)
        {
            object current = blocks[0]; // source block

            for (int i = 1; i < blocks.Count; i++)
            {
                object next = blocks[i];

                Type currentType = current.GetType();

                MethodInfo linkTo = currentType.GetMethod("LinkTo");

                if (linkTo is null)
                {
                    throw new InvalidOperationException($"Method \"LinkTo\" is not found on type {currentType}.");
                }

                try
                {
                    linkTo.Invoke(current, new object[] { next });
                }
                catch (Exception error)
                {
                    throw new InvalidOperationException($"Failed to link {currentType} to {next.GetType()}: {error.Message}.");
                }

                current = next;
            }
        }
        private List<object> CreatePipelineBlocks(in PipelineRecord pipeline)
        {
            int typeCode = _domainModel.GetTypeCode(typeof(ProcessorRecord));

            var processors = _dataSource.Select(typeCode, pipeline.GetEntity());

            List<object> instances = new();

            foreach (var item in processors)
            {
                if (item is ProcessorRecord processor)
                {
                    Type handler = _assemblyManager.Resolve(processor.Handler);

                    if (_registry.TryGetValue(handler, out ServiceCreationInfo info))
                    {
                        object instance = null;

                        if (info.Options is not null && info.Options.Length > 0)
                        {
                            object[] options = new object[info.Options.Length];

                            for (int i = 0; i < info.Options.Length; i++)
                            {
                                Type optionsType = info.Options[i];

                                IOptionsFactory factory = _optionsFactory.GetRequiredFactory(optionsType);

                                options[i] = factory.Create(optionsType, processor.GetEntity());
                            }

                            instance = info.Factory(_serviceProvider, options);
                        }
                        else
                        {
                            instance = ActivatorUtilities.CreateInstance(_serviceProvider, handler);
                        }

                        if (instance is not null)
                        {
                            instances.Add(instance);
                        }
                    }
                }
            }

            return instances;
        }

        public IEnumerable<Type> GetRegisteredHandlers()
        {
            return _registry.Keys;
        }
    }
}