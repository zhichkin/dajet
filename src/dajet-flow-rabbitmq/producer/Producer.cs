using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Buffers;
using System.Text;
using System.Web;

namespace DaJet.Flow.RabbitMQ
{
    public sealed class Producer : TargetBlock<Message>
    {
        #region "PRIVATE FIELDS AND CONSTANTS"

        private int _session;
        private const int SESSION_IS_IDLE = 0;
        private const int SESSION_IS_ACTIVE = 1;
        private const int SESSION_HAS_ERROR = 2;

        private int _disposing; // 0 == false, 1 == true
        private byte[] _buffer; // message body buffer

        private string _last_error_text;
        private const string ERROR_CHANNEL_SHUTDOWN = "Channel shutdown: [{0}] {1}";
        private const string ERROR_CONNECTION_SHUTDOWN = "Connection shutdown: [{0}] {1}";
        private const string ERROR_CONNECTION_IS_BLOCKED = "Connection blocked: {0}";
        private const string ERROR_WAIT_FOR_CONFIRMS = "Wait for confirms interrupted";
        private const string ERROR_PUBLISHER_CONFIRMS = "Publisher confirms nacked";

        private const string HEADER_CC = "CC";
        private const string HEADER_BCC = "BCC";

        #endregion

        private IModel _channel;
        private IConnection _connection;
        private IBasicProperties _properties;
        private readonly ProducerOptions _options;
        public Producer(ProducerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));

            Configure();
        }
        private void Configure()
        {
            ParseTargetUri();
            ConfigureHeader_CarbonCopy();
            ConfigureHeader_BlindCarbonCopy();
        }
        private void ParseTargetUri()
        {
            if (string.IsNullOrWhiteSpace(_options.Target)) { return; }

            Uri uri = new(_options.Target);

            if (uri.Scheme != "amqp") { return; }

            HostName = uri.Host;
            HostPort = uri.Port;

            string[] userpass = uri.UserInfo.Split(':');

            if (userpass != null && userpass.Length == 2)
            {
                UserName = HttpUtility.UrlDecode(userpass[0], Encoding.UTF8);
                Password = HttpUtility.UrlDecode(userpass[1], Encoding.UTF8);
            }

            if (uri.Segments != null && uri.Segments.Length > 1)
            {
                VirtualHost = HttpUtility.UrlDecode(uri.Segments[1].TrimEnd('/'), Encoding.UTF8);
            }
        }
        private void ConfigureHeader_CarbonCopy()
        {
            if (!string.IsNullOrWhiteSpace(_options.CC))
            {
                CarbonCopy = _options.CC.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (CarbonCopy is not null && CarbonCopy.Length == 0)
                {
                    CarbonCopy = null;
                }
            }
            _options.CC = null;
        }
        private void ConfigureHeader_BlindCarbonCopy()
        {
            if (!string.IsNullOrWhiteSpace(_options.BCC))
            {
                BlindCarbonCopy = _options.BCC.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (BlindCarbonCopy is not null && BlindCarbonCopy.Length == 0)
                {
                    BlindCarbonCopy = null;
                }
            }
            _options.BCC = null;
        }

        #region "CONFIGURATION OPTIONS"
        private string HostName { get; set; } = "localhost";
        private int HostPort { get; set; } = 5672;
        private string VirtualHost { get; set; } = "/";
        private string UserName { get; set; } = "guest";
        private string Password { get; set; } = "guest";
        private string[] CarbonCopy { get; set; }
        private string[] BlindCarbonCopy { get; set; }
        #endregion

        #region "RABBITMQ CONNECTION AND CHANNEL"
        private void InitializeConnection()
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
            _connection.ConnectionBlocked += HandleConnectionBlocked;
            _connection.ConnectionUnblocked += HandleConnectionUnblocked;
            _connection.ConnectionShutdown += ConnectionShutdownHandler;
        }
        private void HandleConnectionBlocked(object sender, ConnectionBlockedEventArgs args)
        {
            SetSessionToErrorState(string.Format(ERROR_CONNECTION_IS_BLOCKED, args.Reason));
        }
        private void HandleConnectionUnblocked(object sender, EventArgs args) { /* ? IGNORE ? */ }
        private void ConnectionShutdownHandler(object sender, ShutdownEventArgs args)
        {
            SetSessionToErrorState(string.Format(ERROR_CONNECTION_SHUTDOWN, args.ReplyCode.ToString(), args.ReplyText));
        }

        private void InitializeChannel()
        {
            _channel = _connection.CreateModel();
            _channel.ConfirmSelect();
            _channel.BasicAcks += BasicAcksHandler;
            _channel.BasicNacks += BasicNacksHandler;
            _channel.BasicReturn += BasicReturnHandler;
            _channel.ModelShutdown += ModelShutdownHandler;

            _properties = _channel.CreateBasicProperties();
            _properties.ContentType = "application/json";
            _properties.DeliveryMode = 2; // persistent
            _properties.ContentEncoding = "UTF-8";

            //_channel.FlowControl ? not implemented for C# by RabbitMQ team
            //_channel.BasicRecoverOk ???
        }
        private void BasicAcksHandler(object sender, BasicAckEventArgs args) { /* ? IGNORE ? */ }
        private void BasicNacksHandler(object sender, BasicNackEventArgs args) { /* ? IGNORE ? */ }
        private string GetReturnReason(in BasicReturnEventArgs args)
        {
            return "Message return (" + args.ReplyCode.ToString() + "): " +
                (string.IsNullOrWhiteSpace(args.ReplyText) ? "(empty)" : args.ReplyText) + ". " +
                "Exchange: " + (string.IsNullOrWhiteSpace(args.Exchange) ? "(empty)" : args.Exchange) + ". " +
                "RoutingKey: " + (string.IsNullOrWhiteSpace(args.RoutingKey) ? "(empty)" : args.RoutingKey) + ".";
        }
        private void BasicReturnHandler(object sender, BasicReturnEventArgs args)
        {
            SetSessionToErrorState(GetReturnReason(in args));
        }
        private void ModelShutdownHandler(object sender, ShutdownEventArgs args)
        {
            SetSessionToErrorState(string.Format(ERROR_CHANNEL_SHUTDOWN, args.ReplyCode.ToString(), args.ReplyText));
        }
        #endregion

        private void BeginSessionOrThrow()
        {
            if (Interlocked.CompareExchange(ref _session, SESSION_IS_ACTIVE, SESSION_IS_IDLE) == SESSION_IS_IDLE)
            {
                try
                {
                    InitializeConnection();

                    InitializeChannel();
                }
                catch
                {
                    _ = Interlocked.Exchange(ref _session, SESSION_IS_IDLE);

                    throw;
                }
            }
        }
        private void CloseSessionOrThrow()
        {
            if (_channel.WaitForConfirms())
            {
                int state = Interlocked.CompareExchange(ref _session, SESSION_IS_IDLE, SESSION_IS_ACTIVE);

                if (state != SESSION_IS_ACTIVE)
                {
                    throw new OperationCanceledException(state == SESSION_HAS_ERROR ? _last_error_text : ERROR_WAIT_FOR_CONFIRMS);
                }
            }
            else
            {
                throw new OperationCanceledException(ERROR_PUBLISHER_CONFIRMS);
            }
        }
        private void ThrowIfSessionHasErrors()
        {
            if (Interlocked.CompareExchange(ref _session, SESSION_HAS_ERROR, SESSION_HAS_ERROR) == SESSION_HAS_ERROR)
            {
                throw new InvalidOperationException(_last_error_text);
            }
        }
        private void SetSessionToErrorState(string error)
        {
            if (Interlocked.CompareExchange(ref _session, SESSION_HAS_ERROR, SESSION_IS_ACTIVE) == SESSION_IS_ACTIVE)
            {
                _last_error_text = error;
            }
        }
        private void DisposeProducer()
        {
            _last_error_text = null;

            _properties = null;

            if (_channel is not null)
            {
                _channel.BasicAcks -= BasicAcksHandler;
                _channel.BasicNacks -= BasicNacksHandler;
                _channel.BasicReturn -= BasicReturnHandler;
                _channel.ModelShutdown -= ModelShutdownHandler;
            }

            try { _channel?.Dispose(); }
            catch { /* IGNORE */}
            finally { _channel = null; }

            if (_connection is not null)
            {
                _connection.ConnectionBlocked -= HandleConnectionBlocked;
                _connection.ConnectionUnblocked -= HandleConnectionUnblocked;
                _connection.ConnectionShutdown -= ConnectionShutdownHandler;
            }

            try { _connection?.Dispose(); }
            catch { /* IGNORE */}
            finally { _connection = null; }

            if (_buffer is not null)
            {
                ArrayPool<byte>.Shared.Return(_buffer); _buffer = null;
            }

            _ = Interlocked.Exchange(ref _session, SESSION_IS_IDLE);
        }
        protected override void _Synchronize()
        {
            try
            {
                ThrowIfSessionHasErrors();

                CloseSessionOrThrow();
            }
            catch (NullReferenceException)
            {
                throw new ObjectDisposedException(typeof(Producer).ToString());
            }
            catch
            {
                throw;
            }
            finally
            {
                _Dispose();
            }
        }
        protected override void _Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposing, 1, 0) == 0)
            {
                DisposeProducer();

                _ = Interlocked.Exchange(ref _disposing, 0);
            }
        }

        public override void Process(in Message input)
        {
            try
            {
                ThrowIfSessionHasErrors();

                BeginSessionOrThrow();

                PublishMessageOrThrow(in input);
            }
            catch
            {
                _Dispose(); throw;
            }
        }
        private void PublishMessageOrThrow(in Message message)
        {
            try
            {
                PublishMessage(in message);
            }
            catch (NullReferenceException)
            {
                throw new ObjectDisposedException(typeof(Producer).ToString());
            }
            catch
            {
                throw;
            }
        }
        private void PublishMessage(in Message message)
        {
            ConfigureMessageHeaders(in message);
            ConfigureMessageProperties(in message);

            ReadOnlyMemory<byte> payload = message.Payload.IsEmpty
                ? EncodeMessageBody(message.Body)
                : message.Payload;

            if (string.IsNullOrWhiteSpace(_options.Exchange))
            {
                // clear CC and BCC headers if present
                _ = _properties?.Headers?.Remove("CC"); // carbon copy
                _ = _properties?.Headers?.Remove("BCC"); // blind carbon copy

                // send message directly to the specified queue
                _channel.BasicPublish(string.Empty, _options.RoutingKey, _options.Mandatory, _properties, payload);
            }
            else if (string.IsNullOrWhiteSpace(_options.RoutingKey))
            {
                // send message to the specified exchange
                _channel.BasicPublish(_options.Exchange, string.Empty, _options.Mandatory, _properties, payload);
            }
            else
            {
                // send message to the specified exchange using provided routing key
                _channel.BasicPublish(_options.Exchange, _options.RoutingKey, _options.Mandatory, _properties, payload);
            }
        }
        private void ConfigureMessageHeaders(in Message message)
        {
            if (CarbonCopy is null && BlindCarbonCopy is null && message.Headers.Count == 0) { return; }

            _properties.Headers ??= new Dictionary<string, object>();

            if (!message.Headers.TryGetValue(HEADER_CC, out object cc))
            {
                cc = CarbonCopy;
            }

            if (cc is not null)
            {
                if (!_properties.Headers.TryAdd(HEADER_CC, cc))
                {
                    _properties.Headers[HEADER_CC] = cc;
                }
            }

            if (!message.Headers.TryGetValue(HEADER_BCC, out object bcc))
            {
                bcc = BlindCarbonCopy;
            }

            if (bcc is not null)
            {
                if (!_properties.Headers.TryAdd(HEADER_BCC, bcc))
                {
                    _properties.Headers[HEADER_BCC] = bcc;
                }
            }
        }
        private void ConfigureMessageProperties(in Message message)
        {
            if (!string.IsNullOrEmpty(message.AppId))
            {
                _properties.AppId = message.AppId;
            }
            else if (!string.IsNullOrEmpty(_options.Sender))
            {
                _properties.AppId = _options.Sender;
            }

            if (!string.IsNullOrEmpty(message.Type))
            {
                _properties.Type = message.Type;
            }
            else if (!string.IsNullOrEmpty(_options.MessageType))
            {
                _properties.Type = _options.MessageType;
            }

            if (message.MessageId is not null) { _properties.MessageId = message.MessageId; }
            if (message.CorrelationId is not null) { _properties.CorrelationId = message.CorrelationId; }

            _properties.Priority = message.Priority;
            _properties.DeliveryMode = message.DeliveryMode;
            _properties.ContentType = message.ContentType;
            _properties.ContentEncoding = message.ContentEncoding;

            if (message.ReplyTo is not null) { _properties.ReplyTo = message.ReplyTo; }
            if (message.Expiration is not null) { _properties.Expiration = message.Expiration; }
            if (message.UserId is not null) { _properties.UserId = message.UserId; }
            if (message.ClusterId is not null) { _properties.ClusterId = message.ClusterId; }
        }
        private ReadOnlyMemory<byte> EncodeMessageBody(in string message)
        {
            //return Encoding.UTF8.GetBytes(message);

            int bufferSize = message.Length * 2; // char == 2 bytes

            if (_buffer is null)
            {
                _buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            }
            else if (_buffer.Length < bufferSize)
            {
                ArrayPool<byte>.Shared.Return(_buffer);

                _buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            }

            int encoded = Encoding.UTF8.GetBytes(message, 0, message.Length, _buffer, 0);

            ReadOnlyMemory<byte> payload = new(_buffer, 0, encoded);

            return payload;
        }
    }
}