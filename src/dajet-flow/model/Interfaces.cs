namespace DaJet.Flow
{
    public interface IInputBlock<TInput>
    {
        void Process(in TInput input);
        void Synchronize();
    }
    public interface IOutputBlock<TOutput>
    {
        void LinkTo(in IInputBlock<TOutput> next);
    }
    public interface ISourceBlock
    {
        void Execute();
    }
}