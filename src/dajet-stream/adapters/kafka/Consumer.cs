using Confluent.Kafka;
using DaJet.Data;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Stream.Kafka
{
    public sealed class Consumer : IProcessor
    {
        private IProcessor _next;
        private readonly StreamScope _scope;
        private readonly ConsumeStatement _options;
        private readonly string _target;
        
        private CancellationTokenSource _cts;
        private int _consumed = 0;
        private readonly string _topic;
        private ConsumerConfig _config;
        private IConsumer<byte[], byte[]> _consumer;
        private ConsumeResult<byte[], byte[]> _result;
        private readonly Action<IConsumer<byte[], byte[]>, Error> _errorHandler;
        private readonly Action<IConsumer<byte[], byte[]>, LogMessage> _logHandler;
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
        public Consumer(in StreamScope scope)
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

            if (!_scope.Variables.ContainsKey(_target))
            {
                _scope.Variables.Add(_target, new DataObject(3));
            }

            if (!_scope.TryGetDeclaration(in _target, out _, out DeclareStatement declare))
            {
                throw new InvalidOperationException($"Declaration of {_target} is not found");
            }

            declare.Type.Binding = CreateMessageSchema();

            StreamFactory.MapOptions(in _scope);

            _topic = GetTopic();
            _logHandler = LogHandler;
            _errorHandler = ErrorHandler;

            _next = StreamFactory.CreateStream(in scope);
        }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Synchronize() { _next?.Synchronize(); } //THINK: do consumer really need it ???
        private ConsumerConfig CreateConsumerConfig()
        {
            Dictionary<string, string> config = new();

            foreach (var option in _scope.Variables)
            {
                string key = GetOptionKey(option.Key);

                if (key == "topic" || key == _target) { continue; }

                if (StreamFactory.TryGetOption(in _scope, option.Key, out object value))
                {
                    config.Add(key, value.ToString());
                }
            }

            return new ConsumerConfig(config);
        }
        private List<ColumnExpression> CreateMessageSchema()
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
        private string GetTopic()
        {
            if (StreamFactory.TryGetOption(in _scope, "Topic", out object value))
            {
                return value.ToString();
            }

            return string.Empty;
        }
        public void Process()
        {
            while (true)
            {
                try
                {
                    Consume();
                }
                catch (Exception error)
                {
                    FileLogger.Default.Write($"[Kafka consumer] {ExceptionHelper.GetErrorMessage(error)}");
                }

                try
                {
                    FileLogger.Default.Write("[Kafka consumer] Sleep 10 seconds ...");

                    Task.Delay(TimeSpan.FromSeconds(10)).Wait();
                }
                catch // (OperationCanceledException)
                {
                    // do nothing - host shutdown requested
                }
            }
        }
        private void Consume()
        {
            if (_cts is not null) { return; }

            _cts ??= new CancellationTokenSource();

            try
            {
                _config ??= CreateConsumerConfig();

                _consumer ??= new ConsumerBuilder<byte[], byte[]>(_config)
                    .SetLogHandler(_logHandler)
                    .SetErrorHandler(_errorHandler)
                    .Build();

                _consumer.Subscribe(_topic);
            }
            catch
            {
                DisposeConsumer(); throw;
            }

            _consumed = 0;

            FileLogger.Default.Write($"Consuming messages ...");

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
                    FileLogger.Default.Write($"Consumed {_consumed} messages");

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

                    //TODO: log consumed by timer
                    //FileLogger.Default.Write($"Consumed {_consumed} messages");
                }
            }
            while (_result is not null && _result.Message is not null);
        }
        private void ConsumeMessage(in ConsumeResult<byte[], byte[]> result)
        {
            if (_scope.TryGetValue(_target, out object value))
            {
                if (value is DataObject message)
                {
                    if (result.Message.Key is null)
                    {
                        message.SetValue("Key", string.Empty);
                    }
                    else
                    {
                        message.SetValue("Key", Encoding.UTF8.GetString(result.Message.Key));
                    }

                    if (result.Message.Value is null)
                    {
                        message.SetValue("Value", string.Empty);
                    }
                    else
                    {
                        message.SetValue("Value", Encoding.UTF8.GetString(result.Message.Value));
                    }

                    if (string.IsNullOrEmpty(result.Topic))
                    {
                        message.SetValue("Topic", _topic);
                    }
                    else
                    {
                        message.SetValue("Topic", result.Topic);
                    }
                }
            }

            _next?.Process();

            _next?.Synchronize();

            _consumer.Commit(); //TODO: commit batches

            _consumed++;
        }
        private void LogHandler(IConsumer<byte[], byte[]> _, LogMessage log)
        {
            FileLogger.Default.Write($"[{_topic}] [{log.Name}]: {log.Message}");
        }
        private void ErrorHandler(IConsumer<byte[], byte[]> consumer, Error error)
        {
            FileLogger.Default.Write($"[{_topic}] [{consumer.Name}] [{string.Concat(consumer.Subscription)}]: {error.Reason}");
        }
        public void Dispose()
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

            _next?.Dispose();
        }
    }
}