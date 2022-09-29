namespace DaJet.Pipeline
{
    public abstract class PipelineBlock : IDisposable
    {
        public abstract void Configure(in Dictionary<string, string> options);
        public abstract void Synchronize();
        public abstract void Dispose();
    }
    public interface IInputBlock<TInput>
    {
        void Process(in TInput input);
    }
    public interface IOutputBlock<TOutput>
    {
        void LinkTo(IInputBlock<TOutput> next);
    }
    public abstract class SourceBlock<TOutput> : PipelineBlock, IOutputBlock<TOutput>
    {
        protected IInputBlock<TOutput>? _next;
        public void LinkTo(IInputBlock<TOutput> next) { _next = next; }
        public abstract void Pump();
        public abstract void Stop();

        //protected void _Synchronize()
        //{
        //    _next?.Synchronize();
        //}
        //public void Dispose()
        //{
        //    _Dispose();
        //    _next?.Dispose();
        //}
        //protected virtual void _Dispose()
        //{
        //    // do nothing by default
        //}
    }
    public abstract class TargetBlock<TInput> : PipelineBlock, IInputBlock<TInput>
    {
        public abstract void Process(in TInput input);
    }
    public abstract class ProcessorBlock<TInput> : PipelineBlock, IInputBlock<TInput>, IOutputBlock<TInput>
    {
        private IInputBlock<TInput>? _next;
        public void LinkTo(IInputBlock<TInput> next) { _next = next; }
        public void Process(in TInput input)
        {
            _Process(in input);
            _next?.Process(in input);
        }
        protected abstract void _Process(in TInput input);
    }
    public abstract class TransformerBlock<TInput, TOutput> : PipelineBlock, IInputBlock<TInput>, IOutputBlock<TOutput>
    {
        private IInputBlock<TOutput>? _next;
        public void LinkTo(IInputBlock<TOutput> next) { _next = next; }
        public void Process(in TInput input)
        {
            _Transform(in input, out TOutput output);
            _next?.Process(in output);
        }
        protected abstract void _Transform(in TInput input, out TOutput output);
    }
}