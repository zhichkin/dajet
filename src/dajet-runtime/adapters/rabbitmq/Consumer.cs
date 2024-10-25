using DaJet.Data;
using DaJet.Json;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Timers;
using System.Web;

namespace DaJet.Runtime.RabbitMQ
{
    public sealed class Consumer : IProcessor
    {
        private IProcessor _next;

        private int _state;
        private const int STATE_IS_IDLE = 0;
        private const int STATE_IS_ACTIVE = 1;
        private const int STATE_AUTORESET = 2;
        private const int STATE_DISPOSING = 3;
        private System.Timers.Timer _heartbeat;
        private ManualResetEvent _cancellation;
        private bool CanExecute { get { return Interlocked.CompareExchange(ref _state, STATE_IS_ACTIVE, STATE_IS_IDLE) == STATE_IS_IDLE; } }
        private bool CanDispose { get { return Interlocked.CompareExchange(ref _state, STATE_DISPOSING, STATE_IS_ACTIVE) == STATE_IS_ACTIVE; } }
        private bool CanAutoReset { get { return Interlocked.CompareExchange(ref _state, STATE_AUTORESET, STATE_IS_ACTIVE) == STATE_IS_ACTIVE; } }

        private IModel _channel;
        private IConnection _connection;
        private EventingBasicConsumer _consumer;
        private int _consumed = 0; // in-memory counter
        private string _consumerTag;
        private readonly ScriptScope _scope;
        private readonly ConsumeStatement _options;
        private readonly string _target;
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

            if (!_scope.TrySetValue(_target, new DataObject(9))) // buffer message
            {
                throw new InvalidOperationException($"Variable {_target} is not found");
            }

            if (!_scope.TryGetDeclaration(in _target, out _, out DeclareStatement declare))
            {
                throw new InvalidOperationException($"Declaration of {_target} is not found");
            }
            
            declare.Type.Binding = CreateMessageSchema();

            StreamFactory.MapOptions(in _scope);
        }
        public void Synchronize() { /* do nothing */ }
        public void LinkTo(in IProcessor next) { _next = next; }
        private List<ColumnExpression> CreateMessageSchema()
        {
            return new List<ColumnExpression>()
            {
                new()
                {
                    Alias = nameof(IBasicProperties.Headers),
                    Expression = new VariableReference()
                    {
                        Identifier = _target,
                        Binding = new TypeIdentifier()
                        {
                            Token = TokenType.Object,
                            Binding = typeof(object),
                            Identifier = ParserHelper.GetDataTypeLiteral(typeof(object))
                        }
                    }
                },
                new()
                {
                    Alias = nameof(IBasicProperties.AppId),
                    Expression = new ScalarExpression() { Token = TokenType.String }
                },
                new()
                {
                    Alias = "Body",
                    Expression = new ScalarExpression() { Token = TokenType.String }
                },
                new()
                {
                    Alias = nameof(IBasicProperties.Type),
                    Expression = new ScalarExpression() { Token = TokenType.String }
                },
                new()
                {
                    Alias = nameof(IBasicProperties.ContentType),
                    Expression = new ScalarExpression() { Token = TokenType.String, Literal = "application/json" }
                },
                new()
                {
                    Alias = nameof(IBasicProperties.ContentEncoding),
                    Expression = new ScalarExpression() { Token = TokenType.String, Literal = "UTF-8" }
                },
                new()
                {
                    Alias = nameof(IBasicProperties.ReplyTo),
                    Expression = new ScalarExpression() { Token = TokenType.String }
                },
                new()
                {
                    Alias = nameof(IBasicProperties.MessageId),
                    Expression = new ScalarExpression() { Token = TokenType.String }
                },
                new()
                {
                    Alias = nameof(IBasicProperties.CorrelationId),
                    Expression = new ScalarExpression() { Token = TokenType.String }
                }
            };
        }

        #region "CONFIGURATION OPTIONS"
        private string HostName { get; set; } = "localhost";
        private int HostPort { get; set; } = 5672;
        private string VirtualHost { get; set; } = "/";
        private string UserName { get; set; } = "guest";
        private string Password { get; set; } = "guest";
        private string QueueName { get; set; } = string.Empty;
        private int Heartbeat { get; set; } = 60; // consumer health check
        private uint PrefetchSize { get; set; } = 0; // size of the client buffer in bytes
        private ushort PrefetchCount { get; set; } = 1; // allowed messages on the fly without ack
        #endregion

        #region "MESSAGE OPTIONS AND VALUES"
        private string GetQueueName()
        {
            if (StreamFactory.TryGetOption(in _scope, "QueueName", out object value))
            {
                return value.ToString();
            }

            return string.Empty;
        }
        private int GetHeartbeat()
        {
            if (StreamFactory.TryGetOption(in _scope, "Heartbeat", out object value))
            {
                if (value is not null && int.TryParse(value.ToString(), out int heartbeat))
                {
                    return heartbeat <= 0 ? 60 : heartbeat;
                }
            }

            return 60; // seconds
        }
        private uint GetPrefetchSize()
        {
            if (StreamFactory.TryGetOption(in _scope, "PrefetchSize", out object value))
            {
                if (value is not null && uint.TryParse(value.ToString(), out uint prefetch_size))
                {
                    return prefetch_size < 0 ? 0 : prefetch_size;
                }
            }

            return 0; // size of the client buffer in bytes
        }
        private ushort GetPrefetchCount()
        {
            if (StreamFactory.TryGetOption(in _scope, "PrefetchCount", out object value))
            {
                if (value is not null && ushort.TryParse(value.ToString(), out ushort prefetch_count))
                {
                    return prefetch_count < 0 ? (ushort)1 : prefetch_count;
                }
            }

            return 1; // allowed messages on the fly without ack
        }
        #endregion

        private void InitializeUri()
        {
            Uri uri = _scope.GetUri(_options.Target);

            if (uri.Scheme != "amqp")
            {
                throw new InvalidOperationException($"[URI] amqp scheme expected");
            }

            HostName = uri.Host;
            HostPort = uri.Port;

            string[] userpass = uri.UserInfo.Split(':');

            if (userpass is not null && userpass.Length == 2)
            {
                UserName = HttpUtility.UrlDecode(userpass[0], Encoding.UTF8);
                Password = HttpUtility.UrlDecode(userpass[1], Encoding.UTF8);
            }

            if (uri.Segments is not null && uri.Segments.Length > 1)
            {
                VirtualHost = HttpUtility.UrlDecode(uri.Segments[1].TrimEnd('/'), Encoding.UTF8);
            }
        }
        private void InitializeConsumer()
        {
            QueueName = GetQueueName();
            Heartbeat = GetHeartbeat();
            PrefetchSize = GetPrefetchSize();
            PrefetchCount = GetPrefetchCount();
        }
        private void ReportConsumerStatus()
        {
            int consumed = Interlocked.Exchange(ref _consumed, 0);

            int rate = consumed / Heartbeat;

            FileLogger.Default.Write($"[{QueueName}] Consumed {consumed} / {Heartbeat} = {rate} msg/sec");
        }
        
        private void CreateConsumer()
        {
            IConnectionFactory factory = new ConnectionFactory()
            {
                HostName = HostName,
                Port = HostPort,
                VirtualHost = VirtualHost,
                UserName = UserName,
                Password = Password
            };
            _connection = factory.CreateConnection();

            _channel = _connection.CreateModel();
            _channel.BasicQos(PrefetchSize, PrefetchCount, false);

            _consumer = new EventingBasicConsumer(_channel);
            _consumer.Received += ProcessMessage;
            _consumerTag = _channel.BasicConsume(QueueName, false, _consumer);
        }
        private bool ConsumerIsHealthy()
        {
            return _consumer is not null
                && _consumer.IsRunning
                && _consumer.Model is not null
                && _consumer.Model.IsOpen;
        }
        private void EnsureConsumerIsActive(object sender, ElapsedEventArgs args)
        {
            ReportConsumerStatus();

            if (CanAutoReset)
            {
                try
                {
                    if (!ConsumerIsHealthy())
                    {
                        DisposeConsumer();
                        CreateConsumer();
                    }
                }
                catch (Exception error)
                {
                    DisposeConsumer();

                    FileLogger.Default.Write(ExceptionHelper.GetErrorMessage(error));
                }
                finally
                {
                    _ = Interlocked.Exchange(ref _state, STATE_IS_ACTIVE);
                }
            }
        }
        
        public void Process()
        {
            if (CanExecute)
            {
                InitializeUri();
                InitializeConsumer(); // read context/scope variables

                System.Timers.Timer timer = new();

                if (Interlocked.CompareExchange(ref _heartbeat, timer, null) is not null)
                {
                    timer.Dispose();
                }
                else
                {
                    _heartbeat.AutoReset = true;
                    _heartbeat.Elapsed += EnsureConsumerIsActive;
                    _heartbeat.Interval = TimeSpan.FromSeconds(Heartbeat).TotalMilliseconds;
                }

                ManualResetEvent cancellation = new(false);

                if (Interlocked.CompareExchange(ref _cancellation, cancellation, null) is not null)
                {
                    cancellation.Dispose();
                }

                EnsureConsumerIsActive(this, null);

                _heartbeat.Start(); // periodic consumer health checks
                _cancellation.WaitOne(); // instead of Task.Delay(TimeSpan)
            }
        }
        private void ProcessMessage(object sender, BasicDeliverEventArgs args)
        {
            if (sender is not EventingBasicConsumer consumer) { return; }

            if (_scope.TryGetValue(_target, out object value))
            {
                if (value is DataObject message)
                {
                    ProcessHeaders(in message, in args);
                    message.SetValue("Body", DecodeMessageBody(args.Body));
                    message.SetValue(nameof(IBasicProperties.AppId), args.BasicProperties.AppId ?? string.Empty);
                    message.SetValue(nameof(IBasicProperties.Type), args.BasicProperties.Type ?? string.Empty);
                    message.SetValue(nameof(IBasicProperties.ContentType), args.BasicProperties.ContentType ?? "application/json");
                    message.SetValue(nameof(IBasicProperties.ContentEncoding), args.BasicProperties.ContentEncoding ?? "UTF-8");
                    message.SetValue(nameof(IBasicProperties.ReplyTo), args.BasicProperties.ReplyTo ?? string.Empty);
                    message.SetValue(nameof(IBasicProperties.MessageId), args.BasicProperties.MessageId ?? string.Empty);
                    message.SetValue(nameof(IBasicProperties.CorrelationId), args.BasicProperties.CorrelationId ?? string.Empty);
                }
            }

            try
            {
                _next?.Process();

                _next?.Synchronize();

                consumer.Model.BasicAck(args.DeliveryTag, false);

                Interlocked.Increment(ref _consumed);
            }
            catch (Exception error)
            {
                FileLogger.Default.Write(ExceptionHelper.GetErrorMessage(error));

                NackMessage(in consumer, in args, in error);
            }
        }
        private void ProcessHeaders(in DataObject message, in BasicDeliverEventArgs args)
        {
            IDictionary<string, object> message_headers = args?.BasicProperties?.Headers;

            if (message_headers is null || message_headers.Count == 0)
            {
                return;
            }

            DataObject headers = new(message_headers.Count);

            foreach (var header in message_headers)
            {
                if (header.Key == "CC")
                {
                    continue; // Игнорируем стандартный заголовок
                }

                if (header.Value is bool boolean)
                {
                    headers.SetValue(header.Key, boolean);
                }
                else if (header.Value is int integer)
                {
                    headers.SetValue(header.Key, integer);
                }
                else if (header.Value is decimal number)
                {
                    headers.SetValue(header.Key, number);
                }
                else if (header.Value is byte[] bytes) // this might be whatever ?
                {
                    string value = string.Empty;

                    try
                    {
                        value = Encoding.UTF8.GetString(bytes);
                    }
                    finally
                    {
                        headers.SetValue(header.Key, value);
                    }
                }
                else
                {
                    headers.SetValue(header.Key, null); // unsupported or unknown data type
                }
            }

            if (headers.Count() == 0)
            {
                message.SetValue(nameof(IBasicProperties.Headers), null);
            }
            else
            {
                message.SetValue(nameof(IBasicProperties.Headers), headers);
            }
        }
        private void NackMessage(in EventingBasicConsumer consumer, in BasicDeliverEventArgs args, in Exception error)
        {
            FileLogger.Default.Write(ExceptionHelper.GetErrorMessage(error));

            if (_cancellation is null)
            {
                return; // Consumer is most likely already disposed
            }

            bool signaled = _cancellation.WaitOne(TimeSpan.FromSeconds(Heartbeat));

            if (!signaled) // Consumer is still active
            {
                consumer.Model.BasicNack(args.DeliveryTag, false, true);
            }
        }

        public void Dispose()
        {
            if (CanDispose)
            {
                DisposeHeartbeat();

                DisposeConsumer();

                _next?.Dispose();

                SignalCancellation(); // exit current thread

                _ = Interlocked.Exchange(ref _state, STATE_IS_IDLE);
            }
            else if (Interlocked.CompareExchange(ref _state, STATE_AUTORESET, STATE_AUTORESET) == STATE_AUTORESET)
            {
                Thread.Sleep(1); Dispose();
            }
        }
        private void DisposeConsumer()
        {
            if (_consumer is not null)
            {
                _consumer.Received -= ProcessMessage;
            }

            try { _consumer?.Model?.BasicCancel(_consumerTag); }
            catch { /* IGNORE */ }
            finally { _consumer = null; }

            try { _channel?.Dispose(); }
            catch { /* IGNORE */ }
            finally { _channel = null; }

            try { _connection?.Dispose(); }
            catch { /* IGNORE */ }
            finally { _connection = null; }

            _consumerTag = null;
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
        private void SignalCancellation()
        {
            try
            {
                _ = _cancellation?.Set();
                _cancellation?.Dispose();
            }
            finally
            {
                _cancellation = null;
            }
        }
        
        private static readonly DataObjectJsonConverter _converter = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private static void Transform(in ReadOnlyMemory<byte> message, out DataObject output)
        {
            Utf8JsonReader reader = new(message.Span, true, default);

            output = _converter.Read(ref reader, typeof(DataObject), JsonOptions);
        }
        private static string DecodeMessageBody(in ReadOnlyMemory<byte> message)
        {
            return Encoding.UTF8.GetString(message.Span);
        }
    }
}