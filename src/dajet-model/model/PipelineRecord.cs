using DaJet.Data;
using System;

namespace DaJet.Model
{
    public sealed class PipelineRecord : EntityObject
    {
        private string _name;
        public PipelineRecord(IDataSource source) : base(source) { }
        public string Name { get { return Get(ref _name); } set { Set(value, ref _name); } }
        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? base.ToString() : Name;
        }
        protected override void CopyFrom(in Persistent data)
        {
            if (data is not PipelineRecord source)
            {
                throw new InvalidOperationException();
            }

            _name = source._name;
        }
    }
}