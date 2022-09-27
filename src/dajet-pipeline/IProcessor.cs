namespace DaJet.Pipeline
{
    public interface IProcessor<T> : ISynapse
    {
        void Process(in T input);
    }
    public abstract class Processor<T> : IProcessor<T>, ILinker<T>
    {
        private IProcessor<T>? _next;
        public void LinkTo(IProcessor<T> next)
        {
            _next = next;
        }
        void ISynapse.Synchronize()
        {
            _next?.Synchronize();
        }
        void IDisposable.Dispose()
        {
            _Dispose();
            _next?.Dispose();
        }
        protected virtual void _Dispose()
        {
            // do nothing by default
        }
        public void Process(in T input)
        {
            _Process(in input);
            _next?.Process(in input);
        }
        protected abstract void _Process(in T input);
    }
}