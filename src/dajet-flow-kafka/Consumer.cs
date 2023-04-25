using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DaJet.Flow.Kafka
{
    [PipelineBlock] public sealed class Consumer : SourceBlock<Payload>
    {
        private bool _disposed;
        private int _consumed = 0;
        private int _batchSize = 1000;
        private Message _message;
        private ConsumerConfig _options;
        private IConsumer<Ignore, byte[]> _consumer;
        private ConsumeResult<Ignore, byte[]> _result;
        private readonly Action<IConsumer<Ignore, byte[]>, Error> _errorHandler;
        private readonly Action<IConsumer<Ignore, byte[]>, LogMessage> _logHandler;
        private readonly ILogger _logger;
        [ActivatorUtilitiesConstructor] public Consumer(ILogger<Consumer> logger)
        {
            _logger = logger;
            _logHandler = LogHandler;
            _errorHandler = ErrorHandler;
        }
        #region "CONFIGURATION OPTIONS"
        [Option] public string Topic { get; set; } = string.Empty;
        [Option] public string GroupId { get; set; } = "dajet-group";
        [Option] public string ClientId { get; set; } = "dajet-flow";
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
            _disposed = false;

            _consumer ??= new ConsumerBuilder<Ignore, byte[]>(_options)
                .SetLogHandler(_logHandler)
                .SetErrorHandler(_errorHandler)
                .Build();
            
            _consumer.Subscribe(Topic);

            _consumed = 0;

            do
            {
                _result = _consumer.Consume(TimeSpan.FromSeconds(1));

                if (_result is not null && _result.Message is not null)
                {
                    Payload payload = new(new ReadOnlyMemory<byte>(_result.Message.Value, 0, _result.Message.Value.Length), null);
                    
                    //TODO: _result.Message.Headers; ?

                    _next?.Process(in payload);

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
            while (_result is not null && _result.Message is not null && !_disposed);

            try
            {
                if (_consumed > 0) { _consumer.Commit(); }

                _logger?.LogInformation("[{topic}] Consumed {consumed} messages", Topic, _consumed);

                _consumer.Unsubscribe();
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
        private void LogHandler(IConsumer<Ignore, byte[]> _, LogMessage log)
        {
            _logger?.LogInformation("[{topic}] [{client}]: {message}", Topic, log.Name, log.Message);
        }
        private void ErrorHandler(IConsumer<Ignore, byte[]> consumer, Error error)
        {
            _logger?.LogError("[{topic}] [{consumer}] [{subscription}]: {error}",
                Topic, consumer.Name, string.Concat(consumer.Subscription), error.Reason);
        }
        protected override void _Dispose()
        {
            try
            {
                _consumer?.Close();
                _consumer?.Dispose();
            }
            catch { /* IGNORE */ }
            finally { _consumer = null; }
            
            _result = null;
            _message = null;
            _disposed = true;
        }
    }
}