using System;
using System.Collections.Generic;

namespace DaJet.Metadata.Model
{
    public enum AutomaticUsage
    {
        Use = 0,
        DoNotUse = 1
    }
    public enum SharedPropertyUsage
    {
        Auto = 0,
        Use = 1,
        DoNotUse = 2
    }
    ///<summary>Разделение данных между ИБ</summary>
    public enum DataSeparationUsage
    {
        ///<summary>Разделять</summary>
        Use = 0,
        ///<summary>Не использовать</summary>
        DoNotUse = 1
    }
    ///<summary>Режим использования разделяемых данных</summary>
    public enum DataSeparationMode
    {
        ///<summary>Независимо</summary>
        Independent = 0,
        ///<summary>Независимо и совместно</summary>
        IndependentAndShared = 1
    }
    public sealed class SharedProperty : MetadataProperty
    {
        public AutomaticUsage AutomaticUsage { get; set; }
        public Dictionary<Guid, SharedPropertyUsage> UsageSettings { get; } = new Dictionary<Guid, SharedPropertyUsage>();
        public DataSeparationUsage DataSeparationUsage { get; set; } = DataSeparationUsage.DoNotUse;
        public DataSeparationMode DataSeparationMode { get; set; } = DataSeparationMode.Independent;
    }
}