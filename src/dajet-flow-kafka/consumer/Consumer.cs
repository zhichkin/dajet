using Confluent.Kafka;

namespace DaJet.Flow.Kafka
{
    // https://docs.confluent.io/platform/current/clients/consumer.html
    public sealed class Consumer : SourceBlock<ConsumeResult<byte[], byte[]>>
    {
        private CancellationTokenSource _cts;
        private int _consumed = 0;
        private ConsumerConfig _config;
        private ConsumerOptions _options;
        private IConsumer<byte[], byte[]> _consumer;
        private ConsumeResult<byte[], byte[]> _result;
        private readonly Action<IConsumer<byte[], byte[]>, Error> _errorHandler;
        private readonly Action<IConsumer<byte[], byte[]>, LogMessage> _logHandler;
        private readonly IPipelineManager _manager;
        public Consumer(ConsumerOptions options, IPipelineManager manager)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));

            _config = options.Config;
            _logHandler = LogHandler;
            _errorHandler = ErrorHandler;
        }
        public override void Execute()
        {
            if (_cts is not null) { return; }

            _cts ??= new CancellationTokenSource();

            try
            {
                _consumer ??= new ConsumerBuilder<byte[], byte[]>(_config)
                    .SetLogHandler(_logHandler)
                    .SetErrorHandler(_errorHandler)
                    .Build();

                _consumer.Subscribe(_options.Topic);
            }
            catch
            {
                DisposeConsumer(); throw;
            }

            _consumed = 0;

            do
            {
                try
                {
                    _result = _consumer.Consume(_cts.Token);
                }
                catch (ObjectDisposedException) { /* IGNORE */ }
                catch (OperationCanceledException) { /* IGNORE */ }
                catch
                {
                    DisposeConsumer(); throw; // Unexpected exception
                }

                if (_cts.IsCancellationRequested)
                {
                    _manager?.UpdatePipelineStatus(_options.Pipeline, $"Consumed {_consumed} messages");

                    DisposeConsumer(); return;
                }

                if (_result is not null && _result.Message is not null)
                {
                    try
                    {
                        ConsumeMessage(in _result);
                    }
                    catch
                    {
                        DisposeConsumer(); throw;
                    }

                    _manager?.UpdatePipelineStatus(_options.Pipeline, $"Consumed {_consumed} messages");
                }
            }
            while (_result is not null && _result.Message is not null);
        }
        private void ConsumeMessage(in ConsumeResult<byte[], byte[]> message)
        {
            _next?.Process(in message);

            _next?.Synchronize();

            _consumer.Commit(); //TODO: commit batches

            _consumed++;
        }
        private void LogHandler(IConsumer<byte[], byte[]> _, LogMessage log)
        {
            _manager?.UpdatePipelineStatus(_options.Pipeline, log.Message);
            _manager?.UpdatePipelineFinishTime(_options.Pipeline, DateTime.Now);
            FileLogger.Default.Write($"[{_options.Topic}] [{log.Name}]: {log.Message}");
        }
        private void ErrorHandler(IConsumer<byte[], byte[]> consumer, Error error)
        {
            _manager?.UpdatePipelineStatus(_options.Pipeline, error.Reason);
            _manager?.UpdatePipelineFinishTime(_options.Pipeline, DateTime.Now);
            FileLogger.Default.Write($"[{_options.Topic}] [{consumer.Name}] [{string.Concat(consumer.Subscription)}]: {error.Reason}");
        }
        protected override void _Dispose()
        {
            if (_cts is null || _cts.IsCancellationRequested)
            {
                return;
            }

            _cts?.Cancel(); // interrupt consumption
        }
        private void DisposeConsumer()
        {
            _result = null;

            try
            {
                _consumer?.Close();
                _consumer?.Dispose();
            }
            finally { _consumer = null; }

            try { _cts?.Dispose(); }
            finally { _cts = null; }
        }
    }
}