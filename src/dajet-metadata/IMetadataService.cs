using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;

namespace DaJet.Metadata
{
    public interface IMetadataService : IDisposable
    {
        List<InfoBaseOptions> Options { get; }
        void Add(InfoBaseOptions options);
        void Remove(string key);

        bool TryGetInfoBase(string key, out InfoBase infoBase, out string error);
        bool TryGetMetadataProvider(string key, out IMetadataProvider metadata, out string error);
        bool TryGetOneDbMetadataProvider(string key, out OneDbMetadataProvider metadata, out string error);
    }
}