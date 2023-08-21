using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using Error = Confluent.Kafka.Error;

namespace DaJet.Flow.Kafka
{
    // https://docs.confluent.io/platform/current/clients/producer.html
    [PipelineBlock] public sealed class Producer : TargetBlock<Message<byte[], byte[]>>
    {
        private const string HEADER_TOPIC = "topic";

        private int _produced;
        private string _error;
        private string _topic;
        private ProducerConfig _options;
        private IProducer<byte[], byte[]> _producer;
        private readonly Action<IProducer<byte[], byte[]>, Error> _errorHandler;
        private readonly Action<IProducer<byte[], byte[]>, LogMessage> _logHandler;
        private readonly Action<DeliveryReport<byte[], byte[]>> _deliveryReportHandler;
        private readonly IPipelineManager _manager;
        [ActivatorUtilitiesConstructor] public Producer(IPipelineManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _logHandler = LogHandler;
            _errorHandler = ErrorHandler;
            _deliveryReportHandler = HandleDeliveryReport;
        }
        #region "CONFIGURATION OPTIONS"
        [Option] public Guid Pipeline { get; set; } = Guid.Empty;
        [Option] public string Topic { get; set; } = string.Empty;
        [Option] public string ClientId { get; set; } = "dajet-exchange";
        [Option] public string BootstrapServers { get; set; } = "127.0.0.1:9092";
        [Option] public string Acks { get; set; } = "all";
        [Option] public string MaxInFlight { get; set; } = "1";
        [Option] public string MessageTimeoutMs { get; set; } = "30000";
        [Option] public string EnableIdempotence { get; set; } = "false";
        protected override void _Configure()
        {
            Dictionary<string, string> config = ConfigHelper.CreateConfigFromOptions(this);

            _ = config.Remove(nameof(Topic).ToLower());
            _ = config.Remove(nameof(Pipeline).ToLower());

            _options = new ProducerConfig(config);
        }
        #endregion
        public override void Process(in Message<byte[], byte[]> message)
        {
            _producer ??= new ProducerBuilder<byte[], byte[]>(_options)
                .SetLogHandler(_logHandler)
                .SetErrorHandler(_errorHandler)
                .Build();

            try
            {
                ProcessTopicHeader(in message);

                _producer.Produce(_topic, message, _deliveryReportHandler); // async inside - returns immediately
            }
            catch
            {
                DisposeProducer(); throw;
            }
        }
        private void ProcessTopicHeader(in Message<byte[], byte[]> message)
        {
            _topic = Topic;

            if (message.Headers is null)
            {
                return;
            }

            Headers headers = message.Headers;

            for (int i = 0; i < headers.Count; i++)
            {
                IHeader header = headers[i];

                if (header.Key == HEADER_TOPIC)
                {
                    byte[] value = header.GetValueBytes();

                    if (value is not null && value.Length > 0)
                    {
                        _topic = Encoding.UTF8.GetString(value);
                    }

                    headers.Remove(HEADER_TOPIC);

                    break;
                }
            }
        }
        private void LogHandler(IProducer<byte[], byte[]> _, LogMessage message)
        {
            _manager?.UpdatePipelineStatus(Pipeline, message.Message);
            FileLogger.Default.Write($"[{Topic}] [{message.Name}]: {message.Message}");
        }
        private void ErrorHandler(IProducer<byte[], byte[]> _, Error error)
        {
            if (error is not null)
            {
                _error = error.Reason;
            }
            _manager?.UpdatePipelineStatus(Pipeline, error?.Reason);
            FileLogger.Default.Write($"[{Topic}] [{ClientId}]: {error?.Reason}");
        }
        private void HandleDeliveryReport(DeliveryReport<byte[], byte[]> report)
        {
            if (report.Status == PersistenceStatus.Persisted)
            {
                Interlocked.Increment(ref _produced);
            }
            else if (report.Error is not null && report.Error.Code != ErrorCode.NoError)
            {
                _error = report.Error.Reason; //FIXME: stop producing the batch !?
                _manager?.UpdatePipelineStatus(Pipeline, report.Error.Reason); // show last error
                FileLogger.Default.Write($"[{report.Topic}] [{ClientId}]: {report.Error.Reason}");
            }
        }
        protected override void _Synchronize()
        {
            int produced = _produced;

            try
            {
                _producer?.Flush(); // synchronously wait for pending work to complete
            }
            catch
            {
                throw;
            }
            finally
            {
                _produced = 0;
                DisposeProducer();
            }

            if (_error is not null)
            {
                string error = _error;
                _error = null;
                throw new InvalidOperationException(error);
            }
            else
            {
                _manager?.UpdatePipelineStatus(Pipeline, $"[{ClientId}] Produced {produced} messages");
            }
        }
        private void DisposeProducer()
        {
            try { _producer?.Dispose(); }
            finally { _producer = null; }
        }
    }
}