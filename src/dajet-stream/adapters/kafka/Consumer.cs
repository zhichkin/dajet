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

        private int _state;
        private const int STATE_IDLE = 0;
        private const int STATE_ACTIVE = 1;
        private const int STATE_DISPOSING = 2;
        private AutoResetEvent _sleep;
        private bool CanExecute { get { return Interlocked.CompareExchange(ref _state, STATE_ACTIVE, STATE_IDLE) == STATE_IDLE; } }
        private bool IsActive { get { return Interlocked.CompareExchange(ref _state, STATE_ACTIVE, STATE_ACTIVE) == STATE_ACTIVE; } }
        private bool CanDispose { get { return Interlocked.CompareExchange(ref _state, STATE_DISPOSING, STATE_ACTIVE) == STATE_ACTIVE; } }

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
        private string GetTopic()
        {
            if (StreamFactory.TryGetOption(in _scope, "Topic", out object value))
            {
                return value.ToString();
            }

            return string.Empty;
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
        
        private void LogHandler(IConsumer<byte[], byte[]> _, LogMessage log)
        {
            FileLogger.Default.Write($"[{_topic}] [{log.Name}]: {log.Message}");
        }
        private void ErrorHandler(IConsumer<byte[], byte[]> consumer, Error error)
        {
            FileLogger.Default.Write($"[{_topic}] [{consumer.Name}] [{string.Concat(consumer.Subscription)}]: {error.Reason}");
        }
        
        public void Process()
        {
            if (CanExecute) // STATE_IDLE -> STATE_ACTIVE
            {
                AutoResetEvent sleep = new(false);

                if (Interlocked.CompareExchange(ref _sleep, sleep, null) is not null)
                {
                    sleep.Dispose();
                }

                WhileActiveDoWork(); // STATE_ACTIVE -> STATE_IDLE
            }
        }
        private void WhileActiveDoWork()
        {
            while (IsActive) // STATE_ACTIVE
            {
                try
                {
                    SubscribeConsumer(); // configure consumer and subscribe to topic

                    ConsumeMessages(); // consume messages in "push from broker" mode
                }
                catch (Exception error)
                {
                    FileLogger.Default.Write($"[Kafka consumer] {ExceptionHelper.GetErrorMessage(error)}");
                }

                if (IsActive && _sleep is not null)
                {
                    FileLogger.Default.Write("[Kafka consumer] Sleep 10 seconds ...");

                    bool signaled = _sleep.WaitOne(TimeSpan.FromSeconds(10)); // suspend thread

                    if (signaled) // the Dispose method is called -> STATE_IDLE
                    {
                        FileLogger.Default.Write("[Kafka consumer] Shutdown requested");
                    }
                }
            }
        }
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
        private void SubscribeConsumer()
        {
            _config ??= CreateConsumerConfig();

            _consumer ??= new ConsumerBuilder<byte[], byte[]>(_config)
                .SetLogHandler(_logHandler)
                .SetErrorHandler(_errorHandler)
                .Build();

            _consumer.Subscribe(_topic);
        }
        private void ConsumeMessages()
        {
            _consumed = 0;
            int batch = 0;
            int print = 0;

            do
            {
                _result = _consumer.Consume(10); //TODO: ConsumeTimeout setting

                if (_result is not null && _result.Message is not null)
                {
                    ProcessMessage(in _result);

                    _next?.Process();
                    
                    _consumed++;

                    batch++;
                    print++;

                    if (batch == 1000) //TODO: BatchSize setting
                    {
                        _next?.Synchronize();

                        _ = _consumer.Commit(); // commit consumer offsets

                        batch = 0;
                    }

                    if (print == 10000) //TODO: monitor consumed messages by timer !?
                    {
                        FileLogger.Default.Write($"[Kafka consumer][{_topic}] Consumed {print} messages");

                        print = 0;
                    }
                }
            }
            while (_result is not null && _result.Message is not null && IsActive);

            if (batch > 0)
            {
                _next?.Synchronize();

                _ = _consumer.Commit(); // commit consumer offsets
            }

            FileLogger.Default.Write($"[KafkaMessageConsumer][{_topic}] Consumed {_consumed} messages.");
        }
        private void ProcessMessage(in ConsumeResult<byte[], byte[]> result)
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
        }
        
        public void Synchronize() { /* IGNORE */ }
        public void Dispose()
        {
            if (CanDispose) // STATE_ACTIVE -> STATE_DISPOSING
            {
                _result = null;

                try
                {
                    _consumer?.Close();
                    _consumer?.Dispose();
                }
                finally
                {
                    _consumer = null;
                }

                try
                {
                    _next?.Dispose();
                }
                catch (Exception error)
                {
                    FileLogger.Default.Write(error);
                }

                _ = Interlocked.Exchange(ref _state, STATE_IDLE);
            }
        }
    }
}