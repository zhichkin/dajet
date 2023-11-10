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
        private readonly Dictionary<Type, HandlerDescriptor> _handlers = new();
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

            Type[] options = type.GetHandlerConstructorOptions();

            ObjectFactory factory = ActivatorUtilities.CreateFactory(type, options);

            HandlerDescriptor descriptor = new()
            {
                Service = type,
                Options = options,
                Factory = factory,
                Input = inputType,
                Output = outputType
            };

            _ = _handlers.TryAdd(type, descriptor);
        }
        public Type[] GetRegisteredHandlers()
        {
            return _handlers.Keys.ToArray();
        }
        
        public IPipeline Create(in PipelineRecord record)
        {
            IPipeline pipeline = CreatePipeline(in record);

            List<object> handlers = CreateHandlers(in pipeline, in record);

            if (handlers.Count == 0)
            {
                throw new InvalidOperationException($"Pipeline does not have any handlers.");
            }

            if (handlers[0] is not ISourceBlock handler)
            {
                throw new InvalidOperationException($"Pipeline source type does not implement DaJet.Flow.ISourceBlock interface.");
            }

            AssemblePipeline(handlers);

            pipeline.Initialize(handler);

            return pipeline;
        }
        private IPipeline CreatePipeline(in PipelineRecord record)
        {
            IOptionsFactory<PipelineOptions> factory = _optionsFactory.GetRequiredFactory<PipelineOptions>();

            PipelineOptions options = factory.Create(record.GetEntity());

            object instance = ActivatorUtilities.CreateInstance(_serviceProvider, typeof(Pipeline), options);

            return instance as IPipeline;
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
        private List<object> CreateHandlers(in IPipeline pipeline, in PipelineRecord record)
        {
            var handlers = _dataSource.Query<HandlerRecord>(record.GetEntity());

            List<object> instances = new();

            foreach (HandlerRecord handler in handlers)
            {
                Type handlerType = _assemblyManager.Resolve(handler.Handler);

                if (_handlers.TryGetValue(handlerType, out HandlerDescriptor descriptor))
                {
                    object instance = null;

                    Type[] options = descriptor.Options;

                    if (options is not null && options.Length > 0)
                    {
                        object[] parameters = CreateHandlerOptions(in pipeline, handler.GetEntity(), in options);

                        instance = descriptor.Factory(_serviceProvider, parameters);
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
        private object[] CreateHandlerOptions(in IPipeline pipeline, Entity handler, in Type[] options)
        {
            int count = options.Length;

            object[] instances = new object[count];
            
            for (int i = 0; i < count; i++)
            {
                Type optionsType = options[i];

                if (optionsType == typeof(IPipeline))
                {
                    instances[i] = pipeline;
                }
                else
                {
                    IOptionsFactory factory = _optionsFactory.GetRequiredFactory(optionsType);

                    instances[i] = factory.Create(optionsType, handler);
                }
            }

            return instances;
        }
    }
}