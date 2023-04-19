using DaJet.Flow.Model;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Buffers;
using System.Text;
using System.Web;

namespace DaJet.Flow.RabbitMQ
{
    [PipelineBlock] public sealed class Producer : TargetBlock<Message>
    {
        private int _session;
        private const int SESSION_IS_IDLE = 0;
        private const int SESSION_IS_ACTIVE = 1;
        private const int SESSION_HAS_ERROR = 2;
        private string _last_error_text;

        private int _disposing; // 0 == false, 1 == true
        private IModel _channel;
        private IConnection _connection;
        private IBasicProperties _properties;
        private byte[] _buffer; // message body buffer
        private PublishTracker _tracker; // publisher confirms tracker
        private readonly IPipelineManager _manager;
        [ActivatorUtilitiesConstructor] public Producer(IPipelineManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }
        
        #region "CONFIGURATION OPTIONS"
        [Option] public Guid Pipeline { get; set; } = Guid.Empty;
        [Option] public string Target { get; set; } = "amqp://guest:guest@localhost:5672/%2F";
        [Option] public string Exchange { get; set; } = string.Empty; // if exchange name is empty, then RoutingKey is a queue name to send directly
        [Option] public string RoutingKey { get; set; } = string.Empty; // if exchange name is not empty, then this is routing key value
        [Option] public bool Mandatory { get; set; } = false; // helps to detect unroutable messages, firing BasicReturn event on producer
        
        private string HostName { get; set; } = "localhost";
        private int HostPort { get; set; } = 5672;
        private string VirtualHost { get; set; } = "/";
        private string UserName { get; set; } = "guest";
        private string Password { get; set; } = "guest";

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
        protected override void _Configure() { ParseTargetUri(); }
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
            SetSessionToErrorState($"Connection is blocked: {args.Reason}");
        }
        private void HandleConnectionUnblocked(object sender, EventArgs args) { /* ? IGNORE ? */ }
        private void ConnectionShutdownHandler(object sender, ShutdownEventArgs args)
        {
            SetSessionToErrorState($"Connection shutdown: [{args.ReplyCode}] {args.ReplyText}");
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
        private void BasicAcksHandler(object sender, BasicAckEventArgs args)
        {
            _tracker?.SetAckStatus(args.DeliveryTag, args.Multiple);
        }
        private string GetReturnReason(in BasicReturnEventArgs args)
        {
            string reason = "Message return (" + args.ReplyCode.ToString() + "): " +
                (string.IsNullOrWhiteSpace(args.ReplyText) ? "(empty)" : args.ReplyText) + ". " +
                "Exchange: " + (string.IsNullOrWhiteSpace(args.Exchange) ? "(empty)" : args.Exchange) + ". " +
                "RoutingKey: " + (string.IsNullOrWhiteSpace(args.RoutingKey) ? "(empty)" : args.RoutingKey) + ".";

            if (args.BasicProperties is not null &&
                args.BasicProperties.Headers is not null &&
                args.BasicProperties.Headers.TryGetValue("CC", out object value) && value is not null &&
                value is List<object> recipients && recipients is not null && recipients.Count > 0)
            {
                string cc = string.Empty;

                for (int i = 0; i < recipients.Count; i++)
                {
                    if (i == 10) { cc += ",..."; break; }

                    if (recipients[i] is byte[] recipient)
                    {
                        if (string.IsNullOrEmpty(cc))
                        {
                            cc = Encoding.UTF8.GetString(recipient);
                        }
                        else
                        {
                            cc += "," + Encoding.UTF8.GetString(recipient);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(cc)) { reason += " CC: " + cc; }
            }

            return reason;
        }
        private void BasicNacksHandler(object sender, BasicNackEventArgs args)
        {
            SetSessionToErrorState($"Nack received: [{args.DeliveryTag}]");
        }
        private void BasicReturnHandler(object sender, BasicReturnEventArgs args)
        {
            SetSessionToErrorState(GetReturnReason(in args));
        }
        private void ModelShutdownHandler(object sender, ShutdownEventArgs args)
        {
            SetSessionToErrorState($"Channel shutdown: [{args.ReplyCode}] {args.ReplyText}");
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

                    _tracker = new PublishTracker(1000);
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
            try
            {
                if (!_channel.WaitForConfirms())
                {
                    throw new InvalidOperationException(nameof(Producer));
                }
            }
            catch
            {
                throw;
            }
            finally
            {
                _ = Interlocked.Exchange(ref _session, SESSION_IS_IDLE);
            }
        }
        private void ThrowIfSessionHasError()
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
            _properties = null;

            if (_channel is not null)
            {
                _channel.BasicAcks -= BasicAcksHandler;
                _channel.BasicNacks -= BasicNacksHandler;
                _channel.BasicReturn -= BasicReturnHandler;
                _channel.ModelShutdown -= ModelShutdownHandler;
            }

            try { _channel?.Dispose(); } // causes the ModelShutdownHandler to fire
            finally { _channel = null; } // which invokes SetShutdownStatus on _tracker

            if (_connection is not null)
            {
                _connection.ConnectionBlocked -= HandleConnectionBlocked;
                _connection.ConnectionUnblocked -= HandleConnectionUnblocked;
                _connection.ConnectionShutdown -= ConnectionShutdownHandler;
            }

            try { _connection?.Dispose(); }
            finally { _connection = null; }

            if (_buffer is not null)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = null;
            }

            _tracker?.Clear();
            _tracker = null;
        }
        protected override void _Synchronize()
        {
            try
            {
                ThrowIfSessionHasError();

                CloseSessionOrThrow();
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
        protected override void _Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposing, 1, 0) == 0)
            {
                DisposeProducer();

                _ = Interlocked.Exchange(ref _session, SESSION_IS_IDLE);

                _ = Interlocked.Exchange(ref _disposing, 0);
            }
        }

        public override void Process(in Message input)
        {
            try
            {
                ThrowIfSessionHasError();

                BeginSessionOrThrow();

                PublishMessage(in input);
            }
            catch
            {
                Dispose();
                
                throw;
            }
        }
        private void PublishMessage(in Message input)
        {
            _tracker?.Track(_channel.NextPublishSeqNo);

            CopyMessageProperties(in input);

            ReadOnlyMemory<byte> messageBody = EncodeMessageBody(input.Body);

            if (string.IsNullOrWhiteSpace(Exchange))
            {
                // clear CC and BCC headers if present
                _ = _properties?.Headers?.Remove("CC"); // carbon copy
                _ = _properties?.Headers?.Remove("BCC"); // blind carbon copy

                // send message directly to the specified queue
                _channel.BasicPublish(string.Empty, RoutingKey, Mandatory, _properties, messageBody);
            }
            else if (string.IsNullOrWhiteSpace(RoutingKey))
            {
                // send message to the specified exchange and message type as a routing key 
                _channel.BasicPublish(Exchange, input.Type, Mandatory, _properties, messageBody);
            }
            else
            {
                // send message to the specified exchange and routing key
                _channel.BasicPublish(Exchange, RoutingKey, Mandatory, _properties, messageBody);
            }
        }
        private void CopyMessageProperties(in Message message)
        {
            _properties.Type = message.Type;
            _properties.Headers = message.Headers;

            _properties.DeliveryMode = message.DeliveryMode;
            _properties.ContentType = message.ContentType;
            _properties.ContentEncoding = message.ContentEncoding;

            _properties.AppId = message.AppId;
            _properties.UserId = message.UserId;
            _properties.ClusterId = message.ClusterId;
            _properties.MessageId = message.MessageId;

            _properties.Priority = message.Priority;
            _properties.Expiration = message.Expiration;

            _properties.ReplyTo = message.ReplyTo;
            _properties.CorrelationId = message.CorrelationId;
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

            ReadOnlyMemory<byte> messageBody = new(_buffer, 0, encoded);

            return messageBody;
        }
    }
}