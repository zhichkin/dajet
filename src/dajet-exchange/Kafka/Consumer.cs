﻿using Confluent.Kafka;
using DaJet.Flow;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Text;

namespace DaJet.Exchange.Kafka
{
    [PipelineBlock] public sealed class Consumer : SourceBlock<OneDbMessage>
    {
        private CancellationTokenSource _cts;
        private int _consumed = 0;
        private int _batchSize = 1000;
        private OneDbMessage _message;
        private ConsumerConfig _options;
        private IConsumer<byte[], byte[]> _consumer;
        private ConsumeResult<byte[], byte[]> _result;
        private readonly Action<IConsumer<byte[], byte[]>, Error> _errorHandler;
        private readonly Action<IConsumer<byte[], byte[]>, LogMessage> _logHandler;
        private readonly IPipelineManager _manager;
        [ActivatorUtilitiesConstructor] public Consumer(IPipelineManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _logHandler = LogHandler;
            _errorHandler = ErrorHandler;
        }
        #region "CONFIGURATION OPTIONS"
        [Option] public Guid Pipeline { get; set; } = Guid.Empty;
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
            _ = config.Remove(nameof(Pipeline).ToLower());

            _options = new ConsumerConfig(config);
        }
        #endregion
        public override void Execute()
        {
            if (_cts is not null) { return; }

            _cts ??= new CancellationTokenSource();

            _consumer ??= new ConsumerBuilder<byte[], byte[]>(_options)
                .SetLogHandler(_logHandler)
                .SetErrorHandler(_errorHandler)
                .Build();

            _consumer.Subscribe(Topic);

            _consumed = 0;

            Stopwatch watch = new();
            watch.Start();
            
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
                    _manager?.UpdatePipelineStatus(Pipeline, $"Consumed {_consumed} messages in {watch.ElapsedMilliseconds} ms");

                    DisposeConsumer(); return;
                }

                if (_result is not null && _result.Message is not null)
                {
                    try
                    {
                        ConsumeMessage(_result.Message);
                    }
                    catch
                    {
                        DisposeConsumer(); throw;
                    }

                    _manager?.UpdatePipelineStatus(Pipeline, $"Consumed {_consumed} messages in {watch.ElapsedMilliseconds} ms");
                }
            }
            while (_result is not null && _result.Message is not null);
        }
        private void ConsumeMessage(in Message<byte[], byte[]> message)
        {
            _message ??= new OneDbMessage();

            _message.Sender = ClientId;
            _message.Uuid = Guid.NewGuid();
            _message.Sequence = BitConverter.ToInt64(message.Key);
            _message.TypeName = Topic;
            _message.Payload = Encoding.UTF8.GetString(message.Value);

            _next?.Process(in _message);

            _next?.Synchronize();

            _consumer.Commit(); //TODO: commit batches

            _consumed++;
        }
        private void LogHandler(IConsumer<byte[], byte[]> _, LogMessage log)
        {
            _manager?.UpdatePipelineFinishTime(Pipeline, DateTime.Now);
            _manager?.UpdatePipelineStatus(Pipeline, $"[{Topic}] [{log.Name}]: {log.Message}");
        }
        private void ErrorHandler(IConsumer<byte[], byte[]> consumer, Error error)
        {
            _manager?.UpdatePipelineFinishTime(Pipeline, DateTime.Now);
            _manager?.UpdatePipelineStatus(Pipeline, $"[{Topic}] [{consumer.Name}] [{string.Concat(consumer.Subscription)}]: {error.Reason}");
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
            _message = null;

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