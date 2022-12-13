using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;

namespace DaJet.Metadata.Parsers
{
    public interface IMetadataObjectParser
    {
        void Parse(in ConfigFileOptions options, out MetadataInfo info);
        void Parse(in ConfigFileReader source, Guid uuid, out MetadataInfo target);
        void Parse(in ConfigFileReader source, Guid uuid, out MetadataObject target);
    }
}