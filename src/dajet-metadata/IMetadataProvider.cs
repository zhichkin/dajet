using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;

namespace DaJet.Metadata
{
    public interface IMetadataProvider : IConfigurable
    {
        IEnumerable<MetadataItem> GetMetadataItems(Guid type);
        MetadataObject GetMetadataObject(string metadataName);
    }
}