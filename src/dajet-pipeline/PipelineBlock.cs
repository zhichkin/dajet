namespace DaJet.Pipeline
{
    public interface IInputBlock<TInput>
    {
        void Process(in TInput input);
        void Synchronize();
    }
    public interface IOutputBlock<TOutput>
    {
        void LinkTo(IInputBlock<TOutput> next);
    }
    public abstract class SourceBlock<TOutput> : IOutputBlock<TOutput>
    {
        protected IInputBlock<TOutput>? _next;
        public void LinkTo(IInputBlock<TOutput> next) { _next = next; }
        public abstract void Pump();
        public abstract void Stop();
    }
    public abstract class TargetBlock<TInput> : IInputBlock<TInput>
    {
        public abstract void Process(in TInput input);
        public void Synchronize()
        {
            _Synchronize();
        }
        protected virtual void _Synchronize()
        {
            // do nothing by default
        }
    }
    public abstract class ProcessorBlock<TInput> : IInputBlock<TInput>, IOutputBlock<TInput>
    {
        private IInputBlock<TInput>? _next;
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
    public abstract class TransformerBlock<TInput, TOutput> : IInputBlock<TInput>, IOutputBlock<TOutput>
    {
        private IInputBlock<TOutput>? _next;
        public void LinkTo(IInputBlock<TOutput> next) { _next = next; }
        public void Process(in TInput input)
        {
            _Transform(in input, out TOutput output);
            _next?.Process(in output);
        }
        protected abstract void _Transform(in TInput input, out TOutput output);
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