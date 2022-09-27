namespace DaJet.Pipeline
{
    public interface ILinker<T>
    {
        void LinkTo(IProcessor<T> next);
    }
}