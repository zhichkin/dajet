using DaJet.Flow;
using DaJet.Flow.Model;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Buffers;
using System.Text;
using System.Web;

namespace DaJet.Exchange.RabbitMQ
{
    [PipelineBlock] public sealed class Producer : TargetBlock<OneDbMessage>
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
        [ActivatorUtilitiesConstructor] public Producer() { }
        
        #region "CONFIGURATION OPTIONS"
        [Option] public string Target { get; set; } = "amqp://guest:guest@localhost:5672/%2F";
        [Option] public string Exchange { get; set; } = string.Empty; // if exchange name is empty, then RoutingKey is a queue name to send directly
        [Option] public string RoutingKey { get; set; } = string.Empty; // if exchange name is not empty, then this is routing key value
        [Option] public string CC { get; set; } = string.Empty; // additional routing keys seen by consumers
        [Option] public bool Mandatory { get; set; } = false; // helps to detect unroutable messages, firing BasicReturn event on producer

        private string HostName { get; set; } = "localhost";
        private int HostPort { get; set; } = 5672;
        private string VirtualHost { get; set; } = "/";
        private string UserName { get; set; } = "guest";
        private string Password { get; set; } = "guest";
        private string[] CarbonCopy { get; set; }

        private void ParseTargetUri()
        {
            if (string.IsNullOrWhiteSpace(Target)) { return; }

            Uri uri = new(Target);

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
        protected override void _Configure()
        {
            ParseTargetUri();
            ConfigureHeader_CarbonCopy();
        }
        private void ConfigureHeader_CarbonCopy()
        {
            if (!string.IsNullOrWhiteSpace(CC))
            {
                CarbonCopy = CC.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (CarbonCopy is not null && CarbonCopy.Length == 0)
                {
                    CarbonCopy = null;
                }
            }
        }
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
            if (_channel is null) { return; }

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

        public override void Process(in OneDbMessage input)
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
        private void PublishMessageOrThrow(in OneDbMessage message)
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
        private void PublishMessage(in OneDbMessage message)
        {
            ConfigureMessageHeaders(in message);
            ConfigureMessageProperties(in message);

            ReadOnlyMemory<byte> payload = EncodeMessageBody(message.Payload);

            if (string.IsNullOrWhiteSpace(Exchange))
            {
                // clear CC and BCC headers if present
                _ = _properties?.Headers?.Remove("CC"); // carbon copy
                _ = _properties?.Headers?.Remove("BCC"); // blind carbon copy

                // send message directly to the specified queue = routing key
                _channel.BasicPublish(string.Empty, RoutingKey, Mandatory, _properties, payload);
            }
            else if (string.IsNullOrWhiteSpace(RoutingKey))
            {
                if (message.Subscribers.Contains("deleted")) //FIXME: hack ¯\_(ツ)_/¯
                {
                    // send message to the specified exchange and route it by deleted flag
                    _channel.BasicPublish(Exchange, "deleted", Mandatory, _properties, payload);
                }
                else
                {
                    // send message to the specified exchange and route it by message type
                    _channel.BasicPublish(Exchange, message.TypeName, Mandatory, _properties, payload);
                }
            }
            else
            {
                // send message to the specified exchange and routing key
                _channel.BasicPublish(Exchange, RoutingKey, Mandatory, _properties, payload);
            }
        }
        private void ConfigureMessageHeaders(in OneDbMessage message)
        {
            if (CarbonCopy is null && message.Subscribers.Count == 0) { return; }

            _properties.Headers ??= new Dictionary<string, object>();

            if (CarbonCopy is not null)
            {
                if (!_properties.Headers.TryAdd(HEADER_CC, CarbonCopy))
                {
                    _properties.Headers[HEADER_CC] = CarbonCopy;
                }
            }

            if (message.Subscribers is not null && message.Subscribers.Count > 0)
            {
                if (!_properties.Headers.TryAdd(HEADER_BCC, message.Subscribers.ToArray()))
                {
                    _properties.Headers[HEADER_BCC] = message.Subscribers.ToArray();
                }
            }
        }
        private void ConfigureMessageProperties(in OneDbMessage message)
        {
            if (!string.IsNullOrEmpty(message.Sender))
            {
                _properties.AppId = message.Sender;
            }

            if (!string.IsNullOrEmpty(message.TypeName))
            {
                _properties.Type = message.TypeName;
            }

            _properties.MessageId = message.Sequence.ToString();
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