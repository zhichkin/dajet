using DaJet.Data;
using System;

namespace DaJet.Metadata.Data.Model
{
    public sealed class UnionDef
    {
        public Guid Ref { get; set; }
        public int Code { get; set; }
        public string Name { get; set; }
        public UnionType DataType { get; set; }
        public int Qualifier1 { get; set; }
        public int Qualifier2 { get; set; }
    }
}