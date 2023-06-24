namespace DaJet.Flow
{
    public abstract class AsyncProcessorBlock<TInput> : Configurable, IInputBlock<TInput>, IOutputBlock<TInput>
    {
        protected IInputBlock<TInput> _next;
        public void LinkTo(in IInputBlock<TInput> next) { _next = next; }
        public abstract void Process(in TInput input);
        public void Dispose() { _next?.Dispose(); _Dispose(); }
        protected virtual void _Dispose() { /* do nothing by default */ }
        public void Synchronize() { _next?.Synchronize(); _Synchronize(); }
        protected virtual void _Synchronize() { /* do nothing by default */ }
    }
}