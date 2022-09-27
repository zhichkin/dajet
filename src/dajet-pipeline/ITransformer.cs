namespace DaJet.Pipeline
{
    public interface ITransformer<TInput, TOutput> : IProcessor<TInput>, ILinker<TOutput>
    {
        // proxy interface
    }
    public abstract class Transformer<TInput, TOutput> : ITransformer<TInput, TOutput>
    {
        private IProcessor<TOutput>? _next;
        public void LinkTo(IProcessor<TOutput> next)
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
        public void Process(in TInput input)
        {
            _Transform(in input, out TOutput output);
            _next?.Process(in output);
        }
        protected abstract void _Transform(in TInput input, out TOutput output);
    }
}