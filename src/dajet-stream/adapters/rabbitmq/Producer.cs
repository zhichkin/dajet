using DaJet.Data;
using DaJet.Scripting.Model;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Buffers;
using System.Text;
using System.Web;

namespace DaJet.Stream.RabbitMQ
{
    public sealed class Producer : IProcessor
    {
        private IProcessor _next;

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
        private readonly StreamScope _scope;
        private readonly ProduceStatement _options;
        public Producer(in StreamScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ProduceStatement statement)
            {
                throw new ArgumentException(nameof(ProduceStatement));
            }

            _options = statement;
            
            StreamFactory.MapOptions(in _scope);
        }
        public void LinkTo(in IProcessor next) { _next = next; }

        #region "CONFIGURATION OPTIONS"
        private string HostName { get; set; } = "localhost";
        private int HostPort { get; set; } = 5672;
        private string VirtualHost { get; set; } = "/";
        private string UserName { get; set; } = "guest";
        private string Password { get; set; } = "guest";
        #endregion

        #region "MESSAGE OPTIONS AND VALUES"
        private string GetExchange()
        {
            if (StreamFactory.TryGetOption(in _scope, "Exchange", out object value))
            {
                return value.ToString();
            }

            return string.Empty;
        }
        private string GetRoutingKey()
        {
            if (StreamFactory.TryGetOption(in _scope, "RoutingKey", out object value))
            {
                return value.ToString();
            }

            return string.Empty;
        }
        private bool GetMandatory()
        {
            if (StreamFactory.TryGetOption(in _scope, "Mandatory", out object value))
            {
                if (value is bool boolean)
                {
                    return boolean;
                }
            }

            return false;
        }
        private string GetMessageBody()
        {
            if (StreamFactory.TryGetOption(in _scope, "Body", out object value))
            {
                return value.ToString();
            }

            return string.Empty;
        }
        private string[] GetBlindCopy()
        {
            if (StreamFactory.TryGetOption(in _scope, "BlindCopy", out object value))
            {
                if (value is List<DataObject> list && list.Count > 0)
                {
                    string[] array = new string[list.Count];

                    for (int i = 0; i < list.Count; i++)
                    {
                        array[i] = list[i].GetValue(0).ToString();
                    }

                    return array;
                }
                else
                {
                    return value.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                }
            }

            return null;
        }
        private string[] GetCarbonCopy()
        {
            if (StreamFactory.TryGetOption(in _scope, "CarbonCopy", out object value))
            {
                if (value is List<DataObject> list && list.Count > 0)
                {
                    string[] array = new string[list.Count];

                    for (int i = 0; i < list.Count; i++)
                    {
                        array[i] = list[i].GetValue(0).ToString();
                    }

                    return array;
                }
                else
                {
                    return value.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                }
            }

            return null;
        }
        private string GetAppId()
        {
            if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.AppId), out object value))
            {
                return value.ToString();
            }

            return null;
        }
        private string GetMessageId()
        {
            if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.MessageId), out object value))
            {
                return value.ToString();
            }

            return null;
        }
        private string GetMessageType()
        {
            if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.Type), out object value))
            {
                return value.ToString();
            }

            return null;
        }
        private string GetCorrelationId()
        {
            if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.CorrelationId), out object value))
            {
                return value.ToString();
            }

            return null;
        }
        private byte GetPriority()
        {
            if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.Priority), out object value))
            {
                if (value is not null && byte.TryParse(value.ToString(), out byte priority))
                {
                    return priority;
                }
            }

            return 0;
        }
        private byte GetDeliveryMode()
        {
            if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.DeliveryMode), out object value))
            {
                if (value is not null && byte.TryParse(value.ToString(), out byte mode))
                {
                    return mode;
                }
            }

            return 2; // persistent
        }
        private string GetContentType()
        {
            if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.ContentType), out object value))
            {
                return value.ToString();
            }

            return "application/json";
        }
        private string GetContentEncoding()
        {
            if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.ContentEncoding), out object value))
            {
                return value.ToString();
            }

            return "UTF-8";
        }
        private string GetReplyTo()
        {
            if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.ReplyTo), out object value))
            {
                return value.ToString();
            }

            return null;
        }
        private string GetExpiration()
        {
            if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.Expiration), out object value))
            {
                return value.ToString();
            }

            return null;
        }
        #endregion

        #region "RABBITMQ CONNECTION AND CHANNEL"
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
            _properties.DeliveryMode = 2; // 1 = non-persistent, 2 = persistent
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
                    InitializeUri();

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
        public void Synchronize()
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
                Dispose();
            }
        }
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposing, 1, 0) == 0)
            {
                DisposeProducer();

                _ = Interlocked.Exchange(ref _disposing, 0);
            }
        }

        public void Process()
        {
            try
            {
                ThrowIfSessionHasErrors();

                BeginSessionOrThrow();

                PublishMessageOrThrow();
            }
            catch
            {
                Dispose(); throw;
            }
        }
        private void PublishMessageOrThrow()
        {
            try
            {
                PublishMessage();
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
        private void PublishMessage()
        {
            ConfigureMessageHeaders();
            ConfigureMessageProperties();

            ReadOnlyMemory<byte> payload = EncodeMessageBody(GetMessageBody());

            if (string.IsNullOrWhiteSpace(GetExchange()))
            {
                // clear CC and BCC headers if present
                _ = _properties?.Headers?.Remove(HEADER_CC); // carbon copy
                _ = _properties?.Headers?.Remove(HEADER_BCC); // blind carbon copy

                // send message directly to the specified queue
                _channel.BasicPublish(string.Empty, GetRoutingKey(), GetMandatory(), _properties, payload);
            }
            else if (string.IsNullOrWhiteSpace(GetRoutingKey()))
            {
                // send message to the specified exchange without routing key
                _channel.BasicPublish(GetExchange(), string.Empty, GetMandatory(), _properties, payload);
            }
            else
            {
                // send message to the specified exchange using provided routing key
                _channel.BasicPublish(GetExchange(), GetRoutingKey(), GetMandatory(), _properties, payload);
            }
        }
        private void ConfigureMessageHeaders()
        {
            _properties.Headers?.Clear();

            string[] BlindCopy = GetBlindCopy();
            string[] CarbonCopy = GetCarbonCopy();

            if (BlindCopy is null && CarbonCopy is null)
            {
                return;
            }

            _properties.Headers ??= new Dictionary<string, object>();

            if (BlindCopy is not null)
            {
                _ = _properties.Headers.TryAdd(HEADER_BCC, BlindCopy);
            }

            if (CarbonCopy is not null)
            {
                _ = _properties.Headers.TryAdd(HEADER_CC, CarbonCopy);
            }
        }
        private void ConfigureMessageProperties()
        {
            _properties.AppId = GetAppId();
            _properties.MessageId = GetMessageId();
            _properties.Type = GetMessageType();
            _properties.CorrelationId = GetCorrelationId();
            _properties.Priority = GetPriority();
            _properties.DeliveryMode = GetDeliveryMode();
            _properties.ContentType = GetContentType();
            _properties.ContentEncoding = GetContentEncoding();
            _properties.ReplyTo = GetReplyTo();
            _properties.Expiration = GetExpiration();
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