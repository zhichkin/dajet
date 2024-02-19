namespace DaJet.Stream
{
    public interface IProcessor : IDisposable
    {
        void LinkTo(in IProcessor next);
        void Process();
        void Synchronize();
    }
}