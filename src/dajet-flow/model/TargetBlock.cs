namespace DaJet.Flow
{
    public abstract class TargetBlock<TInput> : IInputBlock<TInput>
    {
        public abstract void Process(in TInput input);
        public void Synchronize() { _Synchronize(); }
        protected virtual void _Synchronize()
        {
            // do nothing by default
        }
        public void Dispose() { _Dispose(); }
        protected virtual void _Dispose()
        {
            // do nothing by default
        }
    }
}