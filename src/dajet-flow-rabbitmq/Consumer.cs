using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text;

namespace DaJet.Flow.RabbitMQ
{
    [PipelineBlock] public sealed class Consumer : SourceBlock<Message>
    {
        private CancellationToken _token;
        private readonly IPipelineManager _manager;
        private IModel _channel;
        private IConnection _connection;
        private EventingBasicConsumer _consumer;
        private int _consumed = 0;
        private string _consumerTag;
        [Option] public Guid Pipeline { get; set; } = Guid.Empty;
        [Option] public string HostName { get; set; } = "localhost";
        [Option] public int PortNumber { get; set; } = 5672;
        [Option] public string VirtualHost { get; set; } = "/";
        [Option] public string UserName { get; set; } = "guest";
        [Option] public string Password { get; set; } = "guest";
        [Option] public string Queue { get; set; } = string.Empty;
        [ActivatorUtilitiesConstructor] public Consumer(IPipelineManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }
        public override void Execute()
        {
            //TODO: _token = token;

            while (!_token.IsCancellationRequested)
            {
                try
                {
                    InitializeConsumer();

                    Task.Delay(TimeSpan.FromSeconds(60), _token).Wait(_token);

                    int consumed = Interlocked.Exchange(ref _consumed, 0);

                    _manager.UpdatePipelineStatus(Pipeline, $"[{Pipeline}] Consumed {consumed} messages in 60 seconds.");
                }
                catch
                {
                    throw;
                }
            }
        }
        public void Dispose()
        {
            if (_consumer != null)
            {
                _consumer.Received -= ProcessMessage;
                _consumer.Model = null;
                _consumer = null;
            }

            try
            {
                _channel?.Dispose();
            }
            catch
            {
                // do nothing
            }
            _channel = null;

            try
            {
                _connection?.Dispose();
            }
            catch
            {
                // do nothing
            }
            _connection = null;
        }

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
        }
        private void InitializeChannel()
        {
            if (_channel is not null && _channel.IsOpen) { return; }

            _channel?.Dispose(); // The channel might be closed, but not disposed yet.

            InitializeConnection();

            _channel = _connection.CreateModel();
            _channel.BasicQos(0, 1, false); // consume messages one-by-one at the channels scope
        }
        private void InitializeConsumer()
        {
            if (_consumer is not null && _consumer.Model is not null && _consumer.Model.IsOpen && _consumer.IsRunning)
            {
                return;
            }

            if (_consumer is not null)
            {
                _consumer.Received -= ProcessMessage;
                _consumer.Model?.Dispose();
                _consumer.Model = null;
            }

            InitializeChannel();

            _consumer = new EventingBasicConsumer(_channel);
            _consumer.Received += ProcessMessage;

            _consumerTag = _channel.BasicConsume(Queue, false, _consumer);

            //_consumer.Model.BasicCancel(_consumerTag); ?
        }
        private void ProcessMessage(object sender, BasicDeliverEventArgs args)
        {
            if (sender is not EventingBasicConsumer consumer) return;

            bool success = true;

            try
            {
                Message message = CreateMessage(in args);

                _next?.Process(in message);

                _next?.Synchronize();

                consumer.Model.BasicAck(args.DeliveryTag, false);

                Interlocked.Increment(ref _consumed);
            }
            catch
            {
                success = false;
            }

            if (!success)
            {
                NackMessage(in consumer, in args);
            }
        }
        private void NackMessage(in EventingBasicConsumer consumer, in BasicDeliverEventArgs args)
        {
            try
            {
                // defensive delay from forever cycle if producer is broken
                Task.Delay(TimeSpan.FromSeconds(60), _token).Wait(_token);

                consumer.Model.BasicNack(args.DeliveryTag, false, true);
            }
            catch
            {
                // do nothing
            }
        }
        private Message CreateMessage(in BasicDeliverEventArgs args)
        {
            Message message = new();

            message.AppId = (args.BasicProperties.AppId ?? string.Empty);
            message.Type = (args.BasicProperties.Type ?? string.Empty);
            message.Body = (args.Body.IsEmpty ? string.Empty : Encoding.UTF8.GetString(args.Body.Span));

            if (args.BasicProperties.Headers is Dictionary<string, object> headers)
            {
                message.Headers = headers;
            }

            // TODO: process headers and other basic properties
            // string headers = GetMessageHeaders(in args);

            return message;
        }
        private string GetMessageHeaders(in BasicDeliverEventArgs args)
        {
            if (args.BasicProperties.Headers == null ||
                args.BasicProperties.Headers.Count == 0)
            {
                return string.Empty;
            }

            Dictionary<string, string> headers = new();

            foreach (var header in args.BasicProperties.Headers)
            {
                if (header.Value is byte[] value)
                {
                    try
                    {
                        headers.Add(header.Key, Encoding.UTF8.GetString(value));
                    }
                    catch
                    {
                        headers.Add(header.Key, string.Empty);
                    }
                }
            }

            if (headers.Count == 0)
            {
                return string.Empty;
            }

            return JsonSerializer.Serialize(headers);
        }
    }
}