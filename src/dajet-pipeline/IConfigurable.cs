namespace DaJet.Pipeline
{
    public interface IConfigurable
    {
        void Configure(Dictionary<string, string> options);
    }
}