namespace DaJet.Pipeline
{
    public interface IConfigurable
    {
        void Configure(in Dictionary<string, string> options);
    }
}