namespace DaJet.Pipeline
{
    public interface IPipeline : IDisposable
    {
        Dictionary<string, object> Context { get; }
        void Execute(CancellationToken cancellationToken);

        //TODO:
        //void Suspend();
        //void Continue();
        //void Close();
    }
    public sealed class Pipeline<T> : IPipeline
    {
        private bool _disposed = false;
        private readonly Dictionary<string, object> _context = new();
        public Pipeline() { }
        public Dictionary<string, object> Context { get { return _context; } }
        public void Execute(CancellationToken cancellationToken)
        {
            //ISource<T> source = Services.GetRequiredService<ISource<T>>();

            //using (source)
            //{
            //    source.Pump(cancellationToken);
            //}
        }
        public void Dispose()
        {
            // FIXME: !?
            // Firstly called from DaJetFlowService on stopping host
            // Secondly called from IServiceProvider Dispose method
            // Thirdly called from IPipelineManager Dispose method

            if (_disposed)
            {
                return;
            }
            
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