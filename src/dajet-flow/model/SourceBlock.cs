namespace DaJet.Flow
{
    public abstract class SourceBlock<TOutput> : Configurable, ISourceBlock, IOutputBlock<TOutput>
    {
        protected IInputBlock<TOutput> _next;
        public void LinkTo(in IInputBlock<TOutput> next) { _next = next; }
        public abstract void Execute();
        public void Dispose()
        {
            _next?.Dispose(); _Dispose();
        }
        protected virtual void _Dispose()
        {
            // do nothing by default
        }
    }
}