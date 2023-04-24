namespace DaJet.Flow
{
    public readonly struct Payload : IDisposable
    {
        private readonly Action _callback;
        public Payload(ReadOnlyMemory<byte> data, Action releaseCallback)
        {
            Data = data;

            _callback = releaseCallback ?? throw new ArgumentNullException(nameof(releaseCallback));
        }
        public readonly ReadOnlyMemory<byte> Data { get; }
        public bool IsEmpty { get { return _callback is null; } }
        public void Dispose() { if (_callback is not null) { _callback(); } }
    }
}