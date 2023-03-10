using DaJet.Data;
using System;

namespace DaJet.Metadata.Data.Model
{
    public sealed class PropertyDef
    {
        public Guid Ref { get; set; }
        public int Code { get; set; }
        public string Name { get; set; }
        public TypeDef Owner { get; set; }
        public int Ordinal { get; set; }
        public UnionType DataType { get; set; }
        public int Qualifier1 { get; set; }
        public int Qualifier2 { get; set; }
        public string ColumnName { get; set; }
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsDbGenerated { get; set; }
    }
}