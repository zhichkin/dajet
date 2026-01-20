using Confluent.Kafka;
using DaJet.Data;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Text;
using System.Timers;

namespace DaJet.Runtime.Kafka
{
    public sealed class Consumer : IProcessor
    {
        private IProcessor _next;
        private readonly ScriptScope _scope;
        private readonly ConsumeStatement _options;
        private readonly string _target;

        private int _consumed = 0;
        private System.Timers.Timer _heartbeat;

        private int _state;
        private const int STATE_IDLE = 0;
        private const int STATE_ACTIVE = 1;
        private const int STATE_DISPOSING = 2;
        private CancellationTokenSource _consumeLoop;
        private bool CanExecute { get { return Interlocked.CompareExchange(ref _state, STATE_ACTIVE, STATE_IDLE) == STATE_IDLE; } }
        private bool IsActive { get { return Interlocked.CompareExchange(ref _state, STATE_ACTIVE, STATE_ACTIVE) == STATE_ACTIVE; } }
        private bool CanDispose { get { return Interlocked.CompareExchange(ref _state, STATE_DISPOSING, STATE_ACTIVE) == STATE_ACTIVE; } }

        private readonly string _topic;
        private readonly string _proto_key;
        private readonly string _proto_value;
        private ConsumerConfig _config;
        private readonly Action<IConsumer<byte[], byte[]>, Error> _errorHandler;
        private readonly Action<IConsumer<byte[], byte[]>, LogMessage> _logHandler;
        private readonly Action<IConsumer<byte[], byte[]>, List<TopicPartition>> _partitionsAssignedHandler;
        private readonly Action<IConsumer<byte[], byte[]>, List<TopicPartitionOffset>> _partitionsRevokedHandler;
        
        private static string GetOptionKey(in string name)
        {
            StringBuilder key = new();

            for (int i = 0; i < name.Length; i++)
            {
                char chr = name[i];

                if (char.IsUpper(chr))
                {
                    if (i > 0) { key.Append('.'); }

                    key.Append(char.ToLowerInvariant(chr));
                }
                else
                {
                    key.Append(chr);
                }
            }

            return key.ToString();
        }
        private static List<ColumnExpression> CreateMessageSchema()
        {
            return new List<ColumnExpression>()
            {
                new()
                {
                    Alias = "Key",
                    Expression = new ScalarExpression() { Token = TokenType.String }
                },
                new()
                {
                    Alias = "Value",
                    Expression = new ScalarExpression() { Token = TokenType.String }
                },
                new()
                {
                    Alias = "Topic",
                    Expression = new ScalarExpression() { Token = TokenType.String }
                }
            };
        }
        
        public Consumer(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ConsumeStatement statement)
            {
                throw new ArgumentException(nameof(ConsumeStatement));
            }

            StreamFactory.BindVariables(in _scope);

            _options = statement;

            if (_options.Into?.Value is VariableReference variable)
            {
                _target = variable.Identifier;
            }

            if (!_scope.TrySetValue(_target, new DataObject(3))) // buffer message
            {
                throw new InvalidOperationException($"Variable {_target} is not found");
            }

            if (!_scope.TryGetDeclaration(in _target, out _, out DeclareStatement declare))
            {
                throw new InvalidOperationException($"Declaration of {_target} is not found");
            }

            declare.Type.Binding = CreateMessageSchema();

            StreamFactory.MapOptions(in _scope);

            _topic = GetTopic();
            _proto_key = GetProtoKey();
            _proto_value = GetProtoValue();
            _logHandler = LogHandler;
            _errorHandler = ErrorHandler;
            _partitionsRevokedHandler = PartitionsRevokedHandler;
            _partitionsAssignedHandler = PartitionsAssignedHandler;

            _config = CreateConsumerConfig();
        }
        public void Synchronize() { /* IGNORE */ }
        public void LinkTo(in IProcessor next) { _next = next; }

        private string GetTopic()
        {
            if (StreamFactory.TryGetOption(in _scope, "Topic", out object value))
            {
                return value.ToString();
            }

            return string.Empty;
        }
        private string GetProtoKey()
        {
            if (StreamFactory.TryGetOption(in _scope, "ProtoKey", out object value))
            {
                return value.ToString();
            }

            return string.Empty;
        }
        private string GetProtoValue()
        {
            if (StreamFactory.TryGetOption(in _scope, "ProtoValue", out object value))
            {
                return value.ToString();
            }

            return string.Empty;
        }
        private ConsumerConfig CreateConsumerConfig()
        {
            Dictionary<string, string> config = new();

            foreach (var option in _scope.Variables)
            {
                string key = GetOptionKey(option.Key);

                if (key == "topic" || key == "proto.key" || key == "proto.value" || key == _target) { continue; }

                if (StreamFactory.TryGetOption(in _scope, option.Key, out object value))
                {
                    config.Add(key, value.ToString());
                }
            }

            return new ConsumerConfig(config);
        }

        private void LogHandler(IConsumer<byte[], byte[]> _, LogMessage log)
        {
            FileLogger.Default.Write($"[{_topic}][{log.Name}]: {log.Message}");
        }
        private void ErrorHandler(IConsumer<byte[], byte[]> consumer, Error error)
        {
            if (error.IsFatal)
            {
                //TODO: !?
            }

            FileLogger.Default.Write($"[{_topic}][{consumer.Name}][{string.Concat(consumer.Subscription)}]: {error.Reason}");
        }
        private void PartitionsAssignedHandler(IConsumer<byte[], byte[]> consumer, List<TopicPartition> partitions)
        {
            StringBuilder text = new();

            if (partitions is null || partitions.Count == 0)
            {
                text.Append("none");
            }
            else
            {
                foreach (TopicPartition partition in partitions)
                {
                    text.Append('[').Append(partition.Partition.Value).Append(']');
                }
            }
            
            FileLogger.Default.Write($"[{_topic}][{consumer.Name}] partitions assigned: {text}");
        }
        private void PartitionsRevokedHandler(IConsumer<byte[], byte[]> consumer, List<TopicPartitionOffset> partitions)
        {
            StringBuilder text = new();

            if (partitions is null || partitions.Count == 0)
            {
                text.Append("none");
            }
            else
            {
                foreach (TopicPartitionOffset partition in partitions)
                {
                    string offset = partition.Offset.Value < 0 ? "-" : partition.Offset.Value.ToString();

                    text.Append($"[{partition.Partition.Value}:{offset}]");
                }
            }

            FileLogger.Default.Write($"[{_topic}][{consumer.Name}] partitions revoked: {text}");
        }

        private void StartHeartbeat()
        {
            _heartbeat = new System.Timers.Timer();
            _heartbeat.AutoReset = true;
            _heartbeat.Elapsed += ReportConsumerStatus;
            _heartbeat.Interval = TimeSpan.FromSeconds(60).TotalMilliseconds;
            _heartbeat.Start();
        }
        private void ReportConsumerStatus(object sender, ElapsedEventArgs args)
        {
            int consumed = Interlocked.Exchange(ref _consumed, 0);

            FileLogger.Default.Write($"[{_topic}] Consumed {consumed} messages");
        }
        private void DisposeHeartbeat()
        {
            try
            {
                _heartbeat?.Stop();
                _heartbeat?.Dispose();
            }
            finally
            {
                _heartbeat = null;
            }
        }

        public void Process()
        {
            if (CanExecute) // STATE_IDLE -> STATE_ACTIVE
            {
                StartHeartbeat();

                while (IsActive) // STATE_ACTIVE
                {
                    _consumeLoop = new CancellationTokenSource();

                    CancellationToken cancellationToken = _consumeLoop.Token;

                    RunConsumeLoop(cancellationToken); // Kafka consumer infinite poll loop

                    if (cancellationToken.IsCancellationRequested) // Dispose is called during poll loop
                    {
                        break; // exit processor working loop
                    }

                    try
                    {
                        FileLogger.Default.Write($"[{_topic}] Sleep 60 seconds ...");

                        Task.Delay(TimeSpan.FromSeconds(60)).Wait(cancellationToken);
                    }
                    catch // (OperationCanceledException)
                    {
                        // do nothing - cancellation requested
                    }

                    if (cancellationToken.IsCancellationRequested) // Dispose is called during sleep
                    {
                        break; // exit processor working loop
                    }

                    try { _consumeLoop.Dispose(); }
                    finally { _consumeLoop = null; }
                }

                // STATE_DISPOSING

                DisposeHeartbeat();

                try { _consumeLoop?.Dispose(); }
                finally { _consumeLoop = null; }

                try
                {
                    _next?.Dispose();
                }
                catch (Exception error)
                {
                    FileLogger.Default.Write(error);
                }

                _ = Interlocked.Exchange(ref _state, STATE_IDLE); // STATE_DISPOSING -> STATE_IDLE
            }
        }
        private void RunConsumeLoop(CancellationToken cancellationToken)
        {
            ConsumeResult<byte[], byte[]> result;

            using (IConsumer<byte[], byte[]> consumer = new ConsumerBuilder<byte[], byte[]>(_config)
                .SetLogHandler(_logHandler)
                .SetErrorHandler(_errorHandler)
                .SetPartitionsRevokedHandler(_partitionsRevokedHandler)
                .SetPartitionsAssignedHandler(_partitionsAssignedHandler)
                .Build())
            {
                try
                {
                    consumer.Subscribe(_topic);
                }
                catch (Exception error)
                {
                    FileLogger.Default.Write($"[{_topic}] Failed to subscribe: {ExceptionHelper.GetErrorMessage(error)}");

                    return; // exit consume loop
                }

                string offsetInfo = "-";

                while (!cancellationToken.IsCancellationRequested) // Kafka consumer infinite poll loop
                {
                    try
                    {
                        result = consumer.Consume(cancellationToken);

                        if (result is null || result.Message is null)
                        {
                            break; // no more messages to process - exit consume loop
                        }
                        else
                        {
                            offsetInfo = $"{result.Partition.Value}:{result.Offset.Value}";
                        }
                    }
                    catch (ConsumeException error)
                    {
                        ConsumeResult<byte[], byte[]> record = error.ConsumerRecord;

                        if (record is not null)
                        {
                            if (record.Offset.Value < 0)
                            {
                                offsetInfo = $"{record.Partition.Value}:-";
                            }
                            else
                            {
                                offsetInfo = $"{record.Partition.Value}:{record.Offset.Value}";
                            }
                        }

                        FileLogger.Default.Write($"[{_topic}][{offsetInfo}] Consume error: [{error.Error.Code}] {error.Error.Reason}");

                        break; // exit consume loop
                    }
                    catch (OperationCanceledException)
                    {
                        FileLogger.Default.Write($"[{_topic}] Consumer shutdown requested");

                        break; // exit consume loop
                    }
                    catch (Exception exception)
                    {
                        FileLogger.Default.Write($"[{_topic}] Unexpected consume error: {ExceptionHelper.GetErrorMessage(exception)}");

                        break; // exit consume loop
                    }

                    try
                    {
                        ProcessMessage(in result);
                    }
                    catch (Exception error)
                    {
                        FileLogger.Default.Write($"[{_topic}][{offsetInfo}] Process message error: {ExceptionHelper.GetErrorMessage(error)}");

                        //_consumer.Seek(_result.TopicPartitionOffset);

                        break; // exit consume loop
                    }

                    try
                    {
                        _next?.Process();
                        _next?.Synchronize();
                    }
                    catch (Exception error)
                    {
                        FileLogger.Default.Write($"[{_topic}][{offsetInfo}] Failed to process message: {ExceptionHelper.GetErrorMessage(error)}");

                        break; // exit consume loop
                    }

                    try
                    {
                        consumer.Commit(result); //THINK: Use EnableAutoOffsetStore = false !?
                    }
                    catch (Exception error)
                    {
                        //THINK: _consumer.Seek(_result.TopicPartitionOffset);

                        FileLogger.Default.Write($"[{_topic}][{offsetInfo}] Commit message error: {ExceptionHelper.GetErrorMessage(error)}");

                        break; // exit consume loop
                    }

                    _ = Interlocked.Increment(ref _consumed);
                }

                try
                {
                    consumer.Close(); // leave consumer group
                }
                catch (KafkaException error)
                {
                    FileLogger.Default.Write($"[{_topic}] Close consumer error: [{error.Error.Code}] {error.Error.Reason}");
                }
            }
        }
        private void ProcessMessage(in ConsumeResult<byte[], byte[]> result)
        {
            if (_scope.TryGetValue(_target, out object target))
            {
                if (target is DataObject message)
                {
                    if (string.IsNullOrEmpty(result.Topic))
                    {
                        message.SetValue("Topic", _topic);
                    }
                    else
                    {
                        message.SetValue("Topic", result.Topic);
                    }

                    if (result.Message.Key is null)
                    {
                        message.SetValue("Key", string.Empty);
                    }
                    else
                    {
                        string key = string.Empty;

                        if (string.IsNullOrEmpty(_proto_key))
                        {
                            key = Encoding.UTF8.GetString(result.Message.Key);
                        }
                        else
                        {
                            key = ProtobufConverter.ConvertProtobufToJson(in _proto_key, result.Message.Key);
                        }

                        message.SetValue("Key", key);
                    }

                    if (result.Message.Value is null)
                    {
                        message.SetValue("Value", string.Empty);
                    }
                    else
                    {
                        string value = string.Empty;

                        if (string.IsNullOrEmpty(_proto_value))
                        {
                            value = Encoding.UTF8.GetString(result.Message.Value);
                        }
                        else
                        {
                            value = ProtobufConverter.ConvertProtobufToJson(in _proto_value, result.Message.Value);
                        }

                        message.SetValue("Value", value);
                    }
                }
            }
        }
        public void Dispose()
        {
            if (CanDispose) // STATE_ACTIVE -> STATE_DISPOSING
            {
                try
                {
                    _consumeLoop?.Cancel(); // request consume loop cancellation
                }
                catch // (ObjectDisposedException)
                {
                    // do nothing
                }

                try // wait to exit consume loop and dispose resources
                {
                    Task.Delay(TimeSpan.FromSeconds(3)).Wait();
                }
                finally //TODO: use ManualResetEvent ???
                {
                    // do nothing
                }
            }
        }
    }
}