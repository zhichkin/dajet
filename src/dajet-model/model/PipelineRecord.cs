using DaJet.Data;

namespace DaJet.Model
{
    public sealed class PipelineRecord : EntityObject
    {
        private string _name = string.Empty;
        private ActivationMode _mode = ActivationMode.Manual;
        public string Name { get { return _name; } set { Set(value, ref _name); } }
        public ActivationMode Activation { get { return _mode; } set { Set(value, ref _mode); } }
        public override string ToString() { return string.IsNullOrEmpty(Name) ? base.ToString() : Name; }
    }
}