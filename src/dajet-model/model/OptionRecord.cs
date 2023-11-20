using DaJet.Data;

namespace DaJet.Model
{
    public sealed class OptionRecord : EntityObject
    {
        private string _name = string.Empty;
        private string _type = string.Empty;
        private string _value = string.Empty;
        private Entity _owner;
        public string Name { get { return _name; } set { Set(value, ref _name); } }
        public string Type { get { return _type; } set { Set(value, ref _type); } }
        public string Value { get { return _value; } set { Set(value, ref _value); } }
        public Entity Owner { get { return _owner; } set { Set(value, ref _owner); } }
        public override string ToString() { return string.IsNullOrEmpty(Name) ? base.ToString() : Name; }
    }
}