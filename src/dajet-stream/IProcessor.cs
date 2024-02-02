namespace DaJet.Stream
{
    public interface IProcessor
    {
        void LinkTo(in IProcessor next);
        void Process();
        void Synchronize();
    }
}