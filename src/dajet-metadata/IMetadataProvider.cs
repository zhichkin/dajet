using DaJet.Data;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;

namespace DaJet.Metadata
{
    public interface IMetadataProvider : IConfigurable
    {
        int YearOffset { get; }
        bool IsRegularDatabase { get; }
        string ConnectionString { get; }
        DatabaseProvider DatabaseProvider { get; }
        IDbConfigurator GetDbConfigurator();
        IQueryExecutor CreateQueryExecutor();
        MetadataItem GetMetadataItem(int typeCode);
        IEnumerable<MetadataItem> GetMetadataItems(Guid type);
        MetadataObject GetMetadataObject(Guid type, Guid uuid);
        MetadataObject GetMetadataObject(string metadataName);
        bool TryGetExtendedInfo(Guid uuid, out MetadataItemEx info);
        bool TryGetEnumValue(in string identifier, out EnumValue value);
    }
}