using DaJet.Data;

namespace DaJet.Model
{
    public sealed class TreeNodeRecord : EntityObject
    {
        private string _name;
        private bool _is_folder;
        private Entity _value;
        private Entity _parent;
        public string Name { get { return _name; } set { Set(value, ref _name); } }
        public bool IsFolder { get { return _is_folder; } set { Set(value, ref _is_folder); } }
        public Entity Value { get { return _value; } set { Set(value, ref _value); } }
        public Entity Parent { get { return _parent; } set { Set(value, ref _parent); } }
        public override string ToString() { return string.IsNullOrEmpty(Name) ? base.ToString() : Name; }
    }
}