namespace DaJet.Flow
{
    public abstract class SourceBlock<TOutput> : ISourceBlock, IOutputBlock<TOutput>
    {
        protected IInputBlock<TOutput> _next;
        public void LinkTo(in IInputBlock<TOutput> next) { _next = next; }
        public abstract void Execute();
    }
}