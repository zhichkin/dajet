using DaJet.Data;

namespace DaJet.Model
{
    public sealed class PipelineBlockRecord : EntityObject
    {
        private string _name;
        public PipelineBlockRecord() { TypeCode = 30; }
        public string Name { get { return _name; } set { Set(value, ref _name); } }
        public override string ToString() { return string.IsNullOrEmpty(Name) ? base.ToString() : Name; }
    }
}