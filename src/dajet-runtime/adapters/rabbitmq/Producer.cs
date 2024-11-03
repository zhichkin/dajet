using DaJet.Data;
using DaJet.Scripting.Model;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Buffers;
using System.Text;
using System.Web;

namespace DaJet.Runtime.RabbitMQ
{
    public sealed class Producer : IProcessor
    {
        private IProcessor _next;

        #region "STATE MANAGEMENT"

        private int _state;
        private const int STATE_IDLE = 0;
        private const int STATE_ACTIVE = 1;
        private const int STATE_BROKEN = 2;
        private const int STATE_DISPOSING = 3;

        private const string ERROR_STATE_IS_BROKEN = "Broken state";
        private const string ERROR_CHANNEL_SHUTDOWN = "Channel shutdown: [{0}] {1}";
        private const string ERROR_CONNECTION_SHUTDOWN = "Connection shutdown: [{0}] {1}";
        private const string ERROR_CONNECTION_IS_BLOCKED = "Connection blocked: {0}";
        private const string ERROR_WAIT_FOR_CONFIRMS = "Wait for confirms interrupted";
        private const string ERROR_PUBLISHER_CONFIRMS = "Publisher confirms nacked";

        private const string HEADER_CC = "CC";
        private const string HEADER_BCC = "BCC";

        #endregion

        private byte[] _buffer;
        private IModel _channel;
        private IConnection _connection;
        private IBasicProperties _properties;
        private readonly ScriptScope _scope;
        private readonly ProduceStatement _options;
        public Producer(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ProduceStatement statement)
            {
                throw new ArgumentException(nameof(ProduceStatement));
            }

            StreamFactory.BindVariables(in _scope);

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
        private DataObject GetHeaders()
        {
            if (StreamFactory.TryGetOption(in _scope, "Headers", out object value) && value is DataObject record)
            {
                return record;
            }

            return null;
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

        #region "CONNECTION AND CHANNEL MANAGEMENT"
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
            SetSessionToBrokenState(string.Format(ERROR_CONNECTION_IS_BLOCKED, args.Reason));
        }
        private void HandleConnectionUnblocked(object sender, EventArgs args)
        {
            FileLogger.Default.Write("Connection unblocked");
        }
        private void ConnectionShutdownHandler(object sender, ShutdownEventArgs args)
        {
            SetSessionToBrokenState(string.Format(ERROR_CONNECTION_SHUTDOWN, args.ReplyCode.ToString(), args.ReplyText));
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
        private static string GetReturnReason(in BasicReturnEventArgs args)
        {
            return "Message return (" + args.ReplyCode.ToString() + "): " +
                (string.IsNullOrWhiteSpace(args.ReplyText) ? "(empty)" : args.ReplyText) + ". " +
                "Exchange: " + (string.IsNullOrWhiteSpace(args.Exchange) ? "(empty)" : args.Exchange) + ". " +
                "RoutingKey: " + (string.IsNullOrWhiteSpace(args.RoutingKey) ? "(empty)" : args.RoutingKey) + ".";
        }
        private void BasicReturnHandler(object sender, BasicReturnEventArgs args)
        {
            SetSessionToBrokenState(GetReturnReason(in args));
        }
        private void ModelShutdownHandler(object sender, ShutdownEventArgs args)
        {
            SetSessionToBrokenState(string.Format(ERROR_CHANNEL_SHUTDOWN, args.ReplyCode.ToString(), args.ReplyText));
        }

        private void BeginSessionOrThrow()
        {
            if (Interlocked.CompareExchange(ref _state, STATE_ACTIVE, STATE_IDLE) == STATE_IDLE)
            {
                try
                {
                    InitializeUri();

                    InitializeConnection();

                    InitializeChannel();
                }
                catch
                {
                    throw;
                }
            }
        }
        private void ThrowIfSessionIsBroken()
        {
            if (Interlocked.CompareExchange(ref _state, STATE_BROKEN, STATE_BROKEN) == STATE_BROKEN)
            {
                throw new InvalidOperationException(ERROR_STATE_IS_BROKEN);
            }
        }
        private void SetSessionToBrokenState(string error)
        {
            if (Interlocked.CompareExchange(ref _state, STATE_BROKEN, STATE_ACTIVE) == STATE_ACTIVE)
            {
                FileLogger.Default.Write(error);
            }
        }
        #endregion

        public void Process()
        {
            try
            {
                ThrowIfSessionIsBroken(); // STATE_BROKEN

                BeginSessionOrThrow(); // STATE_IDLE -> STATE_ACTIVE

                PublishMessageOrThrow(); // STATE_ACTIVE
            }
            catch
            {
                Dispose(); throw; // STATE_DISPOSING -> STATE_IDLE
            }
        }
        private void PublishMessageOrThrow()
        {
            if (Interlocked.CompareExchange(ref _state, STATE_ACTIVE, STATE_ACTIVE) == STATE_ACTIVE)
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
            else
            {
                throw new ObjectDisposedException(typeof(Producer).ToString());
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

            DataObject headers = GetHeaders();
            string[] BlindCopy = GetBlindCopy();
            string[] CarbonCopy = GetCarbonCopy();

            if (headers is null && BlindCopy is null && CarbonCopy is null)
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

            if (headers is not null)
            {
                for (int i = 0; i < headers.Count(); i++)
                {
                    string key = headers.GetName(i);
                    object value = headers.GetValue(i);

                    string text = HeaderSerializer.Serialize(in value);
                    byte[] utf8 = Encoding.UTF8.GetBytes(text);
                    _ = _properties.Headers.TryAdd(key, utf8);
                }
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

        public void Synchronize()
        {
            try
            {
                ThrowIfSessionIsBroken(); // STATE_BROKEN

                ConfirmSessionOrThrow(); // STATE_ACTIVE
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
                Dispose(); // STATE_DISPOSING -> STATE_IDLE
            }
        }
        private void ConfirmSessionOrThrow()
        {
            if (Interlocked.CompareExchange(ref _state, STATE_ACTIVE, STATE_ACTIVE) == STATE_ACTIVE)
            {
                if (_channel.WaitForConfirms())
                {
                    ThrowIfSessionIsBroken(); // STATE_BROKEN
                }
                else
                {
                    throw new OperationCanceledException(ERROR_PUBLISHER_CONFIRMS);
                }
            }
            else
            {
                // STATE_IDLE | STATE_DISPOSING
            }
        }
        
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _state, STATE_DISPOSING, STATE_DISPOSING) == STATE_DISPOSING)
            {
                return;
            }

            _ = Interlocked.Exchange(ref _state, STATE_DISPOSING);

            try
            {
                DisposeProducer();
            }
            finally
            {
                _ = Interlocked.Exchange(ref _state, STATE_IDLE);
            }
        }
        private void DisposeProducer()
        {
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
                ArrayPool<byte>.Shared.Return(_buffer);
                
                _buffer = null;
            }
        }
    }
}