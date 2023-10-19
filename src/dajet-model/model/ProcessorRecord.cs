using DaJet.Data;

namespace DaJet.Model
{
    public sealed class ProcessorRecord : EntityObject
    {
        private int _ordinal;
        private string _name;
        private string _handler;
        private string _message;
        private Entity _pipeline;
        public int Ordinal { get { return _ordinal; } set { Set(value, ref _ordinal); } }
        public string Handler { get { return _handler; } set { Set(value, ref _handler); } }
        public string Message { get { return _message; } set { Set(value, ref _message); } }
        public Entity Pipeline { get { return _pipeline; } set { Set(value, ref _pipeline); } }
        public override string ToString() { return string.IsNullOrEmpty(Handler) ? base.ToString() : Handler; }
    }
}