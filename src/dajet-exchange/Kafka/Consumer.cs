using Confluent.Kafka;
using DaJet.Flow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace DaJet.Exchange.Kafka
{
    [PipelineBlock] public sealed class Consumer : SourceBlock<OneDbMessage>
    {
        private bool _disposed = true;
        private CancellationTokenSource _cts;
        private int _consumed = 0;
        private int _batchSize = 1000;
        private OneDbMessage _message;
        private ConsumerConfig _options;
        private IConsumer<byte[], byte[]> _consumer;
        private ConsumeResult<byte[], byte[]> _result;
        private readonly Action<IConsumer<byte[], byte[]>, Error> _errorHandler;
        private readonly Action<IConsumer<byte[], byte[]>, LogMessage> _logHandler;
        private readonly ILogger _logger;
        [ActivatorUtilitiesConstructor] public Consumer(ILogger<Consumer> logger)
        {
            _logger = logger;
            _logHandler = LogHandler;
            _errorHandler = ErrorHandler;
        }
        #region "CONFIGURATION OPTIONS"
        [Option] public string Topic { get; set; } = string.Empty;
        [Option] public string GroupId { get; set; } = "dajet";
        [Option] public string ClientId { get; set; } = "dajet-exchange";
        [Option] public string BootstrapServers { get; set; } = "127.0.0.1:9092";
        [Option] public string EnableAutoCommit { get; set; } = "false";
        [Option] public string AutoOffsetReset { get; set; } = "earliest";
        [Option] public string SessionTimeoutMs { get; set; } = "60000";
        [Option] public string HeartbeatIntervalMs { get; set; } = "20000";
        protected override void _Configure()
        {
            Dictionary<string, string> config = ConfigHelper.CreateConfigFromOptions(this);

            _ = config.Remove(nameof(Topic).ToLower());

            _options = new ConsumerConfig(config);
        }
        #endregion
        public override void Execute()
        {
            if (!_disposed) { return; }

            _disposed = false;

            _cts = new CancellationTokenSource();

            _consumer ??= new ConsumerBuilder<byte[], byte[]>(_options)
                .SetLogHandler(_logHandler)
                .SetErrorHandler(_errorHandler)
                .Build();

            _consumer.Subscribe(Topic);

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
                    _Dispose(); DisposeConsumer(); throw; // Unexpected exception
                }
                if (_cts.IsCancellationRequested)
                {
                    _Dispose(); DisposeConsumer(); return;
                }

                if (_cts is not null && !_cts.IsCancellationRequested && _result is not null && _result.Message is not null)
                {
                    _message ??= new OneDbMessage();

                    _message.Sender = ClientId;
                    _message.Uuid = Guid.NewGuid();
                    _message.Sequence = BitConverter.ToInt64(_result.Message.Key);
                    _message.TypeName = Topic;
                    _message.Payload = Encoding.UTF8.GetString(_result.Message.Value);

                    _next?.Process(in _message);

                    _next?.Synchronize();

                    _consumed++;

                    if (_consumed == _batchSize)
                    {
                        _consumer.Commit();

                        _logger?.LogInformation("[{topic}] Consumed {consumed} messages", Topic, _consumed);

                        _consumed = 0; // prepare to consume next batch
                    }
                }
            }
            while (_cts is not null && !_cts.IsCancellationRequested && _result is not null && _result.Message is not null && !_disposed);

            try
            {
                if (_consumed > 0) { _consumer.Commit(); }

                _logger?.LogInformation("[{topic}] Consumed {consumed} messages", Topic, _consumed);
            }
            catch (Exception error)
            {
                _logger?.LogError("[{topic}] ERROR: {error}", Topic, error.Message);
            }
            finally
            {
                _Dispose();
            }
        }
        private void LogHandler(IConsumer<byte[], byte[]> _, LogMessage log)
        {
            _logger?.LogInformation("[{topic}] [{client}]: {message}", Topic, log.Name, log.Message);
        }
        private void ErrorHandler(IConsumer<byte[], byte[]> consumer, Error error)
        {
            _logger?.LogError("[{topic}] [{consumer}] [{subscription}]: {error}",
                Topic, consumer.Name, string.Concat(consumer.Subscription), error.Reason);
        }
        protected override void _Dispose()
        {
            if (_disposed) { return; }

            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _next?.Dispose();
            }
            finally
            {
                _cts = null;
                _result = null;
                _message = null;
                _disposed = true;
            }
        }
        private void DisposeConsumer()
        {
            try
            {
                _consumer?.Close();
            }
            catch { /* IGNORE */ }
            finally { _consumer = null; }
        }
    }
}