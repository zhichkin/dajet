using DaJet.Metadata;

namespace DaJet.Stream
{
    internal sealed class Pipeline
    {
        private readonly IMetadataProvider _context;
        internal Pipeline(in IMetadataProvider context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }
        internal IMetadataProvider Context { get { return _context; } }
        internal Dictionary<string, object> Parameters { get; } = new();
        internal List<IProcessor> Processors { get; } = new();
        internal void Execute()
        {
            for (int i = 0; i < Processors.Count - 1; i++)
            {
                Processors[i].LinkTo(Processors[i + 1]);
            }

            Processors[0].Process();
        }
    }
}