namespace DaJet.Flow
{
    public abstract class ProcessorBlock<TInput> : IInputBlock<TInput>, IOutputBlock<TInput>
    {
        private IInputBlock<TInput> _next;
        public void LinkTo(IInputBlock<TInput> next) { _next = next; }
        public void Process(in TInput input)
        {
            _Process(in input);
            _next?.Process(in input);
        }
        protected abstract void _Process(in TInput input);
        public void Synchronize()
        {
            _next?.Synchronize();
            _Synchronize();
        }
        protected virtual void _Synchronize()
        {
            // do nothing by default
        }
    }
}