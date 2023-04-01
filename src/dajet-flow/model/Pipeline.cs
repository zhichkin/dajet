namespace DaJet.Flow
{
    public interface IPipeline : IConfigurable, IDisposable
    {
        Dictionary<string, object> Context { get; }
        void Pump(CancellationToken cancellationToken);
    }
    public sealed class Pipeline : IPipeline
    {
        private bool _disposed = false;
        private readonly ISourceBlock _source;
        private readonly Dictionary<string, object> _context = new();
        public Pipeline(ISourceBlock source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }
        public Dictionary<string, object> Context { get { return _context; } }
        public void Configure(in Dictionary<string, string> options)
        {
            //TODO
        }
        public void Pump(CancellationToken cancellationToken)
        {
            using (_source)
            {
                _source.Pump(cancellationToken);
            }
        }
        public void Dispose()
        {
            // FIXME: !?
            // Firstly called from DaJetFlowService on stopping host
            // Secondly called from IServiceProvider Dispose method
            // Thirdly called from IPipelineManager Dispose method

            if (_disposed) { return; }

            _disposed = true; // See comment below

            try
            {
                Context.Clear();

                // Service provider invokes Dispose method on all services
                // in the container, including this pipeline service instance.

                //(Services as IDisposable)?.Dispose();
            }
            catch
            {
                // do nothing
            }
        }
    }
}