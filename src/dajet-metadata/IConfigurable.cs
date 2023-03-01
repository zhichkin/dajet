using System.Collections.Generic;

namespace DaJet
{
    public interface IConfigurable
    {
        void Configure(in Dictionary<string, string> options);
    }
}