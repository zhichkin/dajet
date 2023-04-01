using System.Reflection;

namespace DaJet.Flow
{
    public sealed class PipelineBuilder
    {
        private readonly IPipelineManager _manager;
        public PipelineBuilder(IPipelineManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }
        public IPipeline Build(in PipelineOptions options)
        {
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

            IPipeline pipeline = new Pipeline(source);

            pipeline.Configure(options.Options);

            return pipeline;
        }
        private List<object> ResolvePipelineBlocks(in List<PipelineBlock> blocks)
        {
            List<object> instances = new();

            foreach (PipelineBlock block in blocks)
            {
                Type handler = _manager.ResolveHandler(block.Handler);

                object instance = Activator.CreateInstance(handler);

                if (instance is IConfigurable configurable)
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
    }
}