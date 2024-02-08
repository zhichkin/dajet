using DaJet.Metadata;
using DaJet.Scripting;

namespace DaJet.Stream
{
    internal sealed class PipelineBuilder
    {
        private readonly List<IMetadataProvider> _context = new();
        private readonly List<TranspilerResult> _results = new();
        internal PipelineBuilder() { }
        internal void Add(in IMetadataProvider context, in TranspilerResult result)
        {
            _context.Add(context);
            _results.Add(result);
        }
        internal List<IProcessor> Build(in Dictionary<string, object> parameters)
        {
            List<IProcessor> processors = new();

            for (int i = 0; i < _results.Count; i++)
            {
                TranspilerResult result = _results[i];
                IMetadataProvider context = _context[i];

                processors.AddRange(StreamProcessor.CreatePipeline(in context, in result, in parameters));
            }

            for (int i = 0; i < processors.Count - 1; i++)
            {
                processors[i].LinkTo(processors[i + 1]);
            }

            return processors;
        }
    }
}