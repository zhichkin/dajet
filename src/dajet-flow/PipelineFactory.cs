using DaJet.Data;
using DaJet.Model;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace DaJet.Flow
{
    public interface IPipelineFactory
    {
        Type[] GetRegisteredHandlers();
        IPipeline Create(in PipelineRecord record);
    }
    public sealed class PipelineFactory : IPipelineFactory
    {
        private readonly IDataSource _dataSource;
        private readonly IAssemblyManager _assemblyManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly OptionsFactoryProvider _optionsFactory;
        private readonly Dictionary<Type, ServiceCreationInfo> _registry = new();
        public PipelineFactory(
            IDataSource dataSource, IServiceProvider serviceProvider,
            IAssemblyManager assemblyManager, OptionsFactoryProvider optionsFactory)
        {
            _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
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
                RegisterHandler(type);
            }
        }
        private void RegisterHandler(Type type)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (!type.IsHandler(out Type inputType, out Type outputType))
            {
                return;
            }

            Type[] options = type.GetConstructorOptions();

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
        public Type[] GetRegisteredHandlers()
        {
            return _registry.Keys.ToArray();
        }
        
        public IPipeline Create(in PipelineRecord record)
        {
            List<object> handlers = CreateHandlers(in record);

            if (handlers.Count == 0)
            {
                throw new InvalidOperationException($"Pipeline does not have any handlers.");
            }

            if (handlers[0] is not ISourceBlock source)
            {
                throw new InvalidOperationException($"Pipeline source type does not implement DaJet.Flow.ISourceBlock interface.");
            }

            AssemblePipeline(handlers);

            IOptionsFactory<PipelineOptions> factory = _optionsFactory.GetRequiredFactory<PipelineOptions>();

            PipelineOptions options = factory.Create(record.GetEntity());

            object[] parameters = new object[] { options, source };
            
            object instance = ActivatorUtilities.CreateInstance(_serviceProvider, typeof(Pipeline), parameters);

            if (instance is not IPipeline pipeline)
            {
                return null;
            }

            return pipeline;
        }
        private void AssemblePipeline(in List<object> handlers)
        {
            object current = handlers[0]; // source block

            for (int i = 1; i < handlers.Count; i++)
            {
                object next = handlers[i];

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
        private List<object> CreateHandlers(in PipelineRecord pipeline)
        {
            var handlers = _dataSource.Query<HandlerRecord>(pipeline.GetEntity());

            List<object> instances = new();

            foreach (HandlerRecord handler in handlers)
            {
                Type handlerType = _assemblyManager.Resolve(handler.Handler);

                if (_registry.TryGetValue(handlerType, out ServiceCreationInfo info))
                {
                    object instance = null;

                    if (info.Options is not null && info.Options.Length > 0)
                    {
                        object[] options = new object[info.Options.Length];

                        for (int i = 0; i < info.Options.Length; i++)
                        {
                            Type optionsType = info.Options[i];

                            IOptionsFactory factory = _optionsFactory.GetRequiredFactory(optionsType);

                            OptionsBase optionsInstance = factory.Create(optionsType, handler.GetEntity());

                            if (optionsInstance is HandlerOptions handlerOptions)
                            {
                                handlerOptions.Pipeline = pipeline.Identity;
                            }

                            options[i] = optionsInstance;
                        }

                        instance = info.Factory(_serviceProvider, options);
                    }
                    else
                    {
                        instance = ActivatorUtilities.CreateInstance(_serviceProvider, handlerType);
                    }

                    if (instance is not null)
                    {
                        instances.Add(instance);
                    }
                }
            }

            return instances;
        }
    }
}