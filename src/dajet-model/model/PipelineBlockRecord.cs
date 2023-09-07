using DaJet.Data;
using System;

namespace DaJet.Model
{
    public sealed class PipelineBlockRecord : EntityObject
    {
        private string _name;
        public PipelineBlockRecord(IDataSource source) : base(source) { }
        public PipelineBlockRecord(IDataSource source, Guid identity) : base(source, identity) { }
        public string Name { get { return Get(ref _name); } set { Set(value, ref _name); } }
        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? base.ToString() : Name;
        }
    }
}