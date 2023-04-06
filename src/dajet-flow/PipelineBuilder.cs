using DaJet.Flow.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Runtime.Loader;

namespace DaJet.Flow
{
    public interface IPipelineBuilder
    {
        IPipeline Build(in PipelineOptions options);
        List<PipelineBlock> GetPipelineBlocks();
        List<OptionInfo> GetOptions(Type ownerType);
        List<OptionInfo> GetOptions(string ownerTypeName);
    }
    public sealed class PipelineBuilder : IPipelineBuilder
    {
        private readonly ILogger _logger;
        private readonly IServiceProvider _services;
        private readonly Dictionary<string, Assembly> _assemblies = new();
        public PipelineBuilder(IServiceProvider services, ILogger<PipelineBuilder> logger)
        {
            _logger = logger;
            _services = services;
        }
        private void InitializeAssemblies()
        {
            foreach (string filePath in Directory.GetFiles(AppContext.BaseDirectory, "DaJet.Flow.*"))
            {
                if (_assemblies.ContainsKey(filePath))
                {
                    continue;
                }

                if (Path.GetExtension(filePath) == ".dll")
                {
                    Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(filePath);

                    if (assembly is not null)
                    {
                        _assemblies.Add(filePath, assembly);
                    }

                    _logger?.LogInformation($"[{assembly.FullName}] loaded successfully.");
                }
            }
        }
        public Type ResolveHandler(string name)
        {
            Type type;

            foreach (Assembly assembly in _assemblies.Values)
            {
                type = assembly.GetType(name);

                if (type is not null)
                {
                    return type;
                }
            }

            throw new InvalidOperationException($"Failed to resolve type by name [{name}]");
        }
        public IPipeline Build(in PipelineOptions options)
        {
            InitializeAssemblies();

            List<object> blocks = ResolvePipelineBlocks(options.Blocks);

            if (blocks.Count == 0)
            {
                throw new InvalidOperationException($"Pipeline does not have any blocks.");
            }
            
            AssemblePipeline(blocks);

            if (blocks[0] is not ISourceBlock source)
            {
                throw new InvalidOperationException($"Pipeline source type does not implement DaJet.Flow.ISourceBlock interface.");
            }

            Pipeline pipeline = ActivatorUtilities.CreateInstance(_services, typeof(Pipeline), options.Uuid, source) as Pipeline;

            pipeline.Configure(options.Options);

            return pipeline;
        }
        private List<object> ResolvePipelineBlocks(in List<PipelineBlock> blocks)
        {
            List<object> instances = new();

            foreach (PipelineBlock block in blocks)
            {
                Type handler = ResolveHandler(block.Handler);

                object instance = ActivatorUtilities.CreateInstance(_services, handler);

                if (instance is Configurable configurable)
                {
                    configurable.Configure(block.Options);
                }

                instances.Add(instance);
            }

            return instances;
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

        //

        public List<PipelineBlock> GetPipelineBlocks()
        {
            List<PipelineBlock> blocks = new();

            foreach (Assembly assembly in _assemblies.Values)
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (!(type.IsPublic && type.IsSealed))
                    {
                        continue;
                    }

                    //TODO: PipelineBlockAttribute ???

                    //if (type.IsSubclassOf(typeof(SourceBlock<>)) ||
                    //    type.IsSubclassOf(typeof(TargetBlock<>)) ||
                    //    type.IsSubclassOf(typeof(ProcessorBlock<>)) ||
                    //    type.IsSubclassOf(typeof(TransformerBlock<,>)))
                    //{
                    //}

                    if (type == typeof(Pipeline)) { continue; } // is also Configurable

                    if (type.IsSubclassOf(typeof(Configurable)))
                    {
                        blocks.Add(new PipelineBlock()
                        {
                            Handler = type.ToString(),
                            Message = GetPipelineBlockMessageType(type).ToString()
                        });
                    }
                }
            }

            return blocks;
        }
        public List<OptionInfo> GetOptions(Type ownerType)
        {
            List<OptionInfo> options = new();

            foreach (PropertyInfo property in ownerType.GetProperties())
            {
                if (property.GetCustomAttribute<OptionAttribute>() is not null)
                {
                    options.Add(new OptionInfo()
                    {
                        Name = property.Name,
                        Type = property.PropertyType.ToString()
                    });
                }
            }

            return options;
        }
        public List<OptionInfo> GetOptions(string ownerTypeName)
        {
            Type ownerType = ResolveHandler(ownerTypeName);

            return GetOptions(ownerType);
        }
        private Type GetPipelineBlockMessageType(Type blockType)
        {
            if (blockType.IsGenericType)
            {
                Type[] arguments = blockType.GetGenericArguments();

                return (arguments.Length > 1) ? arguments[1] : arguments[0];
            }

            Type baseType = blockType.BaseType;

            if (baseType is not null && baseType.IsGenericType)
            {
                Type[] arguments = baseType.GetGenericArguments();

                return (arguments.Length > 1) ? arguments[1] : arguments[0];
            }

            return null;
        }
        private Type GetPipelineBlockMessageType(string blockTypeName)
        {
            Type blockType = ResolveHandler(blockTypeName);
            
            return GetPipelineBlockMessageType(blockType);
        }
    }
}