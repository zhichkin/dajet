namespace DaJet.Pipeline
{
    public abstract class Target<T> : IProcessor<T>
    {
        public void Process(in T input)
        {
            _Process(in input);
        }
        protected abstract void _Process(in T input);
        void ISynapse.Synchronize()
        {
            _Synchronize();
        }
        protected virtual void _Synchronize()
        {
            return; // do nothing
        }
        void IDisposable.Dispose()
        {
            _Dispose();
        }
        protected virtual void _Dispose()
        {
            // do nothing
        }
    }
}