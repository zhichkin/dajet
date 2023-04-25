using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Error = Confluent.Kafka.Error;

namespace DaJet.Flow.Kafka
{
    [PipelineBlock] public sealed class Producer : TargetBlock<Payload>
    {
        private int _produced;
        private string _error;
        private ProducerConfig _options;
        private IProducer<Null, byte[]> _producer;
        private readonly Message<Null, byte[]> _message = new();
        private readonly Action<IProducer<Null, byte[]>, Error> _errorHandler;
        private readonly Action<IProducer<Null, byte[]>, LogMessage> _logHandler;
        private readonly Action<DeliveryReport<Null, byte[]>> _deliveryReportHandler;
        private readonly ILogger _logger;
        [ActivatorUtilitiesConstructor] public Producer(ILogger<Producer> logger)
        {
            _logger = logger;
            _logHandler = LogHandler;
            _errorHandler = ErrorHandler;
            _deliveryReportHandler = HandleDeliveryReport;
        }
        #region "CONFIGURATION OPTIONS"
        [Option] public string Topic { get; set; } = string.Empty;
        [Option] public string ClientId { get; set; } = "dajet-flow";
        [Option] public string BootstrapServers { get; set; } = "127.0.0.1:9092";
        [Option] public string Acks { get; set; } = "all";
        [Option] public string MaxInFlight { get; set; } = "1";
        [Option] public string MessageTimeoutMs { get; set; } = "30000";
        [Option] public string EnableIdempotence { get; set; } = "false";
        protected override void _Configure()
        {
            Dictionary<string, string> config = ConfigHelper.CreateConfigFromOptions(this);

            _ = config.Remove(nameof(Topic).ToLower());

            _options = new ProducerConfig(config);
        }
        #endregion
        public override void Process(in Payload input)
        {
            _producer ??= new ProducerBuilder<Null, byte[]>(_options)
                .SetLogHandler(_logHandler)
                .SetErrorHandler(_errorHandler)
                .Build();

            _message.Value = input.Data.ToArray();
            
            //TODO: _message.Headers = input.Headers; ?

            try
            {
                _producer.Produce(Topic, _message, _deliveryReportHandler);
            }
            catch
            {
                _Dispose(); throw;
            }
            finally
            {
                input.Dispose();
            }
        }
        private void LogHandler(IProducer<Null, byte[]> _, LogMessage message)
        {
            _logger?.LogInformation(message.Message);
        }
        private void ErrorHandler(IProducer<Null, byte[]> _, Error error)
        {
            _logger?.LogError(error.Reason);
        }
        private void HandleDeliveryReport(DeliveryReport<Null, byte[]> report)
        {
            if (report.Status == PersistenceStatus.Persisted)
            {
                Interlocked.Increment(ref _produced);
            }
            else if (report.Error is not null && report.Error.Code != ErrorCode.NoError)
            {
                if (!string.IsNullOrWhiteSpace(_error))
                {
                    _error = report.Error.Reason;
                }
                _logger?.LogError(report.Error.Reason);
            }
        }
        protected override void _Synchronize()
        {
            _producer?.Flush();

            int produced = Interlocked.Exchange(ref _produced, 0);

            _logger?.LogInformation("[{topic}] Produced {produced} messages", Topic, produced);

            _message.Value = null;
            _message.Headers = null;

            if (_error is not null)
            {
                string error = _error;

                _error = null;

                throw new InvalidOperationException(error);
            }

            _error = null;
        }
        protected override void _Dispose()
        {
            try { _producer?.Dispose(); }
            catch { /* IGNORE */ }
            finally { _producer = null; }
            
            _error = null;
            _produced = 0;
            _message.Value = null;
            _message.Headers = null;
        }
    }
}