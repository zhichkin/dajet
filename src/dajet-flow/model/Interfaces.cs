namespace DaJet.Flow
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
    public interface ISourceBlock : IDisposable
    {
        void Pump(CancellationToken cancellationToken);
    }
}