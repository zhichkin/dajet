namespace DaJet.Runtime
{
    public interface IProcessor : IDisposable
    {
        void LinkTo(in IProcessor next);
        void Process();
        void Synchronize();
    }
}