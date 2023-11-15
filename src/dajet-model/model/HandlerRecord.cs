using DaJet.Data;

namespace DaJet.Model
{
    public sealed class HandlerRecord : EntityObject
    {
        private int _ordinal;
        private string _name;
        private Entity _pipeline;
        public int Ordinal { get { return _ordinal; } set { Set(value, ref _ordinal); } }
        public string Name { get { return _name; } set { Set(value, ref _name); } }
        public Entity Pipeline { get { return _pipeline; } set { Set(value, ref _pipeline); } }
        public override string ToString() { return string.IsNullOrEmpty(Name) ? base.ToString() : Name; }
    }
}