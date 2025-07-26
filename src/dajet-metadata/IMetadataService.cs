using DaJet.Metadata.Model;
using DaJet.Model;
using System;

namespace DaJet.Metadata
{
    public interface IMetadataService : IDisposable
    {
        void Add(InfoBaseOptions options);
        void Remove(string key);

        bool TryGetInfoBase(in InfoBaseRecord record, out InfoBase infoBase, out string error);
        bool TryGetOrCreate(in InfoBaseRecord record, out IMetadataProvider provider, out string error);
        bool TryGetOrCreate(in InfoBaseOptions options, out IMetadataProvider provider, out string error);
    }
}