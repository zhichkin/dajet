namespace DaJet.Flow
{
    public interface ISourceBlock : IDisposable
    {
        void Execute();
    }
    public interface IOutputBlock<TOutput>
    {
        void LinkTo(in IInputBlock<TOutput> next);
    }
    public interface IInputBlock<TInput> : IDisposable
    {
        void Process(in TInput input);
        void Synchronize();
    }
}