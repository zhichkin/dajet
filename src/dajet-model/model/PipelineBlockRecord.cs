using DaJet.Data;
using System;

namespace DaJet.Model
{
    public sealed class PipelineBlockRecord : EntityObject
    {
        private string _name;
        public PipelineBlockRecord(IDataSource source) : base(source) { }
        public string Name { get { return Get(ref _name); } set { Set(value, ref _name); } }
        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? base.ToString() : Name;
        }
        protected override void CopyFrom(in Persistent data)
        {
            if (data is not PipelineBlockRecord source)
            {
                throw new InvalidOperationException();
            }

            _name = source._name;
        }
    }
}