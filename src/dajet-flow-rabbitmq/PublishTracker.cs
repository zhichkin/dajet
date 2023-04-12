using System.Collections.Concurrent;

namespace DaJet.Flow.RabbitMQ
{
    public enum PublishStatus { New, Ack, Nack }
    internal sealed class PublishTracker
    {
        private const string NACKED_ERROR_MESSAGE = "[PublishTracker] Some messages were nacked.";
        private const string UNEXPECTED_ERROR_MESSAGE = "[PublishTracker] Unexpected publish error.";

        private long _nack = 0L;
        private long _returned = 0L;
        private long _shutdown = 0L;
        private string _reason = string.Empty;

        private readonly object _lock = new();

        private readonly ConcurrentDictionary<ulong, PublishStatus> _tags;

        internal PublishTracker(int capacity) { _tags = new(1, capacity); }

        internal void Track(ulong deliveryTag)
        {
            _ = _tags.TryAdd(deliveryTag, PublishStatus.New);
        }
        internal void Clear() { _tags.Clear(); }

        internal void SetAckStatus(ulong deliveryTag, bool multiple)
        {
            if (multiple)
            {
                SetMultipleStatus(deliveryTag, PublishStatus.Ack);
            }
            else
            {
                SetSingleStatus(deliveryTag, PublishStatus.Ack);
            }
        }
        internal void SetNackStatus(ulong deliveryTag, bool multiple)
        {
            if (multiple)
            {
                SetMultipleStatus(deliveryTag, PublishStatus.Nack);
            }
            else
            {
                SetSingleStatus(deliveryTag, PublishStatus.Nack);
            }
        }
        internal void SetReturnedStatus(string reason)
        {
            Interlocked.Increment(ref _returned);

            _reason = reason;
        }
        internal void SetShutdownStatus(string reason)
        {
            Interlocked.Increment(ref _shutdown);

            Clear();

            _reason = reason;
        }
        internal void SetSingleStatus(ulong deliveryTag, PublishStatus status)
        {
            if (IsNacked || IsShutdown)
            {
                return;
            }

            if (status == PublishStatus.Ack)
            {
                _ = _tags.TryRemove(deliveryTag, out _);
            }
            else if (status == PublishStatus.Nack)
            {
                Interlocked.Increment(ref _nack); Clear();
            }
        }
        internal void SetMultipleStatus(ulong deliveryTag, PublishStatus status)
        {
            if (IsNacked || IsShutdown)
            {
                return;
            }

            if (status == PublishStatus.Ack)
            {
                lock (_lock)
                {
                    List<ulong> remove = new();

                    foreach (var item in _tags)
                    {
                        if (item.Key <= deliveryTag)
                        {
                            remove.Add(item.Key);
                        }
                    }

                    foreach (ulong key in remove)
                    {
                        _ = _tags.TryRemove(key, out _);
                    }
                }
            }
            else if (status == PublishStatus.Nack)
            {
                Interlocked.Increment(ref _nack); Clear();
            }
        }

        internal bool IsNacked { get { return (Interlocked.Read(ref _nack) > 0); } }
        internal bool IsReturned { get { return (Interlocked.Read(ref _returned) > 0); } }
        internal bool IsShutdown { get { return (Interlocked.Read(ref _shutdown) > 0); } }
        internal string ErrorReason { get { return string.IsNullOrWhiteSpace(_reason) ? NACKED_ERROR_MESSAGE : _reason; } }
        internal bool HasErrors()
        {
            if (IsShutdown || IsNacked) { return true; }

            if (!_tags.IsEmpty) { _reason = UNEXPECTED_ERROR_MESSAGE; return true; }

            return false;
        }
    }
}