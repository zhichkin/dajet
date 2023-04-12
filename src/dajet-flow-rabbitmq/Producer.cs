using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Buffers;
using System.Text;

namespace DaJet.Flow.RabbitMQ
{
    [PipelineBlock] public sealed class Producer : TargetBlock<Message>
    {
        private IModel _channel;
        private IConnection _connection;
        private IBasicProperties _properties;
        private bool _connectionIsBlocked = false;
        private byte[] _buffer; // message body buffer
        private PublishTracker _tracker = new(1000); // publisher confirms tracker
        [Option] public Guid Pipeline { get; set; } = Guid.Empty;
        [Option] public string HostName { get; set; } = "localhost";
        [Option] public int PortNumber { get; set; } = 5672;
        [Option] public string VirtualHost { get; set; } = "/";
        [Option] public string UserName { get; set; } = "guest";
        [Option] public string Password { get; set; } = "guest";
        [Option] public string Exchange { get; set; } = string.Empty;
        [Option] public string RoutingKey { get; set; } = string.Empty;
        [Option] public bool Mandatory { get; set; } = false;
        [ActivatorUtilitiesConstructor] public Producer() { }

        #region "RABBITMQ CONNECTION AND CHANNEL SETUP"
        private void InitializeConnection()
        {
            if (_connection is not null && _connection.IsOpen) { return; }

            _connection?.Dispose(); // The connection might be closed, but not disposed yet.

            IConnectionFactory factory = new ConnectionFactory()
            {
                HostName = HostName,
                Port = PortNumber,
                VirtualHost = VirtualHost,
                UserName = UserName,
                Password = Password
            };

            _connection = factory.CreateConnection();
            _connection.ConnectionBlocked += HandleConnectionBlocked;
            _connection.ConnectionUnblocked += HandleConnectionUnblocked;

            //TODO: _connection.ConnectionShutdown += HandleConnectionShutdown; ?
        }
        private void HandleConnectionBlocked(object sender, ConnectionBlockedEventArgs args)
        {
            _connectionIsBlocked = true;
        }
        private void HandleConnectionUnblocked(object sender, EventArgs args)
        {
            _connectionIsBlocked = false;
        }
        private void InitializeChannel()
        {
            if (_channel is not null && _channel.IsOpen) { return; }

            _channel?.Dispose(); // The channel might be closed, but not disposed yet.

            InitializeConnection();

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
        private void BasicNacksHandler(object sender, BasicNackEventArgs args)
        {
            _tracker?.SetNackStatus(args.DeliveryTag, args.Multiple);
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
        private void BasicReturnHandler(object sender, BasicReturnEventArgs args)
        {
            if (_tracker is not null && _tracker.IsReturned) { return; } // already marked as returned

            string reason = GetReturnReason(in args);

            _tracker?.SetReturnedStatus(reason);
        }
        private void ModelShutdownHandler(object sender, ShutdownEventArgs args)
        {
            _connectionIsBlocked = true;

            _tracker?.SetShutdownStatus($"Channel shutdown ({args.ReplyCode}): {args.ReplyText}");
        }
        #endregion

        private void ThrowIfConnectionIsBlocked()
        {
            if (!_connectionIsBlocked) { return; }

            Dispose();

            throw new Exception("Connection is blocked or channel is shutdown by broker.");
        }
        private void ThrowIfChannelIsNotHealthy()
        {
            try
            {
                InitializeChannel();
            }
            catch
            {
                Dispose(); throw;
            }
        }
        protected override void _Synchronize()
        {
            if (_channel is null) { throw new InvalidOperationException(nameof(_channel)); }

            if (_channel.WaitForConfirms())
            {
                //_Dispose(); ? or wait for source consumer to dispose ?
                return;
            }

            if (_tracker.HasErrors()) { throw new InvalidOperationException(_tracker.ErrorReason); }

            _tracker.Clear(); // prepare for the next publish session

            Dispose();
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
            int bufferSize = message.Length * 2; // char == 2 bytes

            if (_buffer == null)
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
        public override void Process(in Message input)
        {
            ThrowIfConnectionIsBlocked();
            ThrowIfChannelIsNotHealthy();

            CopyMessageProperties(in input);

            ReadOnlyMemory<byte> messageBody = EncodeMessageBody(input.Body); //Encoding.UTF8.GetBytes(input.Body);

            _tracker.Track(_channel.NextPublishSeqNo);

            if (string.IsNullOrWhiteSpace(Exchange))
            {
                // clear CC and BCC headers if present
                _ = _properties?.Headers?.Remove("CC"); // carbon copy
                _ = _properties?.Headers?.Remove("BCC"); // blind carbon copy

                // send message directly to the specified queue
                _channel!.BasicPublish(string.Empty, RoutingKey, Mandatory, _properties, messageBody);
            }
            else if (string.IsNullOrWhiteSpace(RoutingKey))
            {
                // send message to the specified exchange and message type as a routing key 
                _channel!.BasicPublish(Exchange, input.Type, Mandatory, _properties, messageBody);
            }
            else
            {
                // send message to the specified exchange and routing key
                _channel!.BasicPublish(Exchange, RoutingKey, Mandatory, _properties, messageBody);
            }
        }
        public void Dispose()
        {
            _properties = null;
            if (_channel is not null) { _channel.Dispose(); _channel = null; }
            if (_connection is not null) { _connection.Dispose(); _connection = null; }
            if (_tracker is not null) { _tracker.Clear(); _tracker = null; }
            if (_buffer is not null) { ArrayPool<byte>.Shared.Return(_buffer); }
        }
    }
}