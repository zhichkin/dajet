using System;

namespace DaJet.Model
{
    public static class SystemTypeCode
    {
        public static readonly int Entity = 0;
        public static readonly int Metadata = 1;
        public static readonly int TypeDef = 2;
        public static readonly int PropertyDef = 3;
        public static readonly int RelationDef = 4;
    }
    public static class SystemTypeUuid
    {
        public static readonly Guid Entity = new("B34412E3-B9BA-46B4-887D-961204543E91");
        public static readonly Guid Metadata = new("43ED3777-C00B-4E45-9D8B-5783771C184B");
        public static readonly Guid TypeDef = new("B910A137-D045-4C74-9BF5-D8C781F36C2C");
        public static readonly Guid PropertyDef = new("DF207406-A318-4AD8-858A-2250D0B485B8");
        public static readonly Guid RelationDef = new("4945B787-20CB-4CDF-AAE8-6E00A19A4CD8");
    }
}