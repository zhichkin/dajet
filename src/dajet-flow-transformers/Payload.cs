namespace DaJet.Flow
{
    public readonly struct Payload : IDisposable
    {
        private readonly Action _callback;
        public Payload(ReadOnlyMemory<byte> data, Action callback)
        {
            Data = data; _callback = callback;
        }
        public readonly ReadOnlyMemory<byte> Data { get; }
        public bool IsEmpty { get { return Data.IsEmpty; } }
        public void Dispose() { if (_callback is not null) { _callback(); } }
    }
}