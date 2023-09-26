using DaJet.Data;

namespace DaJet.Model
{
    public sealed class PipelineRecord : EntityObject
    {
        private string _name;
        public PipelineRecord() { TypeCode = 20; }
        public string Name { get { return _name; } set { Set(value, ref _name); } }
        public override string ToString() { return string.IsNullOrEmpty(Name) ? base.ToString() : Name; }
    }
}