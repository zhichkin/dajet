namespace DaJet.Pipeline
{
    public interface ISource<T> : ILinker<T>, IDisposable
    {
        void Pump(CancellationToken token);
        //TODO: void Stop(); !
    }
    public abstract class Source<T> : ISource<T>
    {
        private IProcessor<T>? _next;
        public void LinkTo(IProcessor<T> processor)
        {
            _next = processor;
        }
        public abstract void Pump(CancellationToken token);
        protected void _Process(in T output)
        {
            _next?.Process(in output);
        }
        protected void _Synchronize()
        {
            _next?.Synchronize();
        }
        public void Dispose()
        {
            _Dispose();
            _next?.Dispose();
        }
        protected virtual void _Dispose()
        {
            // do nothing by default
        }
    }
}