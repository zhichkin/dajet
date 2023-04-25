using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Timers;
using System.Web;

namespace DaJet.Flow.RabbitMQ
{
    [PipelineBlock] public sealed class Consumer : SourceBlock<Message>
    {
        private int _state;
        private const int STATE_IS_IDLE = 0;
        private const int STATE_IS_ACTIVE = 1;
        private const int STATE_AUTORESET = 2;
        private const int STATE_DISPOSING = 3;
        private System.Timers.Timer _heartbeat;
        private bool CanExecute { get { return Interlocked.CompareExchange(ref _state, STATE_IS_ACTIVE, STATE_IS_IDLE) == STATE_IS_IDLE; } }
        private bool CanDispose { get { return Interlocked.CompareExchange(ref _state, STATE_DISPOSING, STATE_IS_ACTIVE) == STATE_IS_ACTIVE; } }
        private bool CanAutoReset { get { return Interlocked.CompareExchange(ref _state, STATE_AUTORESET, STATE_IS_ACTIVE) == STATE_IS_ACTIVE; } }

        private IModel _channel;
        private IConnection _connection;
        private EventingBasicConsumer _consumer;
        private Message _message;
        private int _consumed = 0;
        private string _consumerTag;
        private readonly IPipelineManager _manager;
        [ActivatorUtilitiesConstructor] public Consumer(IPipelineManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        #region "CONFIGURATION OPTIONS"
        [Option] public Guid Pipeline { get; set; } = Guid.Empty;
        [Option] public string Source { get; set; } = "amqp://guest:guest@localhost:5672/%2F";
        [Option] public string Queue { get; set; } = string.Empty;
        [Option] public int Heartbeat { get; set; } = 60; // seconds
        private string HostName { get; set; } = "localhost";
        private int HostPort { get; set; } = 5672;
        private string VirtualHost { get; set; } = "/";
        private string UserName { get; set; } = "guest";
        private string Password { get; set; } = "guest";
        private void ParseSourceUri()
        {
            if (string.IsNullOrWhiteSpace(Source)) { return; }

            Uri uri = new(Source);

            if (uri.Scheme != "amqp") { return; }

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
        protected override void _Configure()
        {
            ParseSourceUri();

            if (Heartbeat < 10) { Heartbeat = 10; }
        }
        #endregion

        private void UpdatePipelineStatus()
        {
            int consumed = Interlocked.Exchange(ref _consumed, 0);
            _manager.UpdatePipelineStatus(Pipeline, $"Consumed {consumed} messages in {Heartbeat} seconds.");
        }
        private bool ConsumerIsHealthy()
        {
            return (_consumer is not null && _consumer.Model is not null && _consumer.Model.IsOpen && _consumer.IsRunning);
        }
        private void InitializeConsumer()
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
            _channel.BasicQos(0, 1, false); // consume any size messages one-by-one at the channels scope

            _consumer = new EventingBasicConsumer(_channel);
            _consumer.Received += ProcessMessage;
            _consumerTag = _channel.BasicConsume(Queue, false, _consumer);
        }
        private void EnsureConsumerIsActive(object sender, ElapsedEventArgs args)
        {
            UpdatePipelineStatus();

            if (CanAutoReset)
            {
                try
                {
                    if (!ConsumerIsHealthy())
                    {
                        DisposeConsumer();
                        InitializeConsumer();
                    }
                }
                catch (Exception error)
                {
                    DisposeConsumer();
                    _manager.UpdatePipelineStatus(Pipeline, error.Message);
                }
                finally
                {
                    _ = Interlocked.Exchange(ref _state, STATE_IS_ACTIVE);
                }
            }
        }
        public override void Execute()
        {
            if (CanExecute)
            {
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

                _heartbeat.Start();
            }
        }
        
        private void ProcessMessage(object sender, BasicDeliverEventArgs args)
        {
            if (sender is not EventingBasicConsumer consumer) { return; }

            _message ??= new Message();
            _message.Payload = new Payload(args.Body, null);
            _message.AppId = args.BasicProperties.AppId;
            _message.Type = args.BasicProperties.Type;
            _message.ReplyTo = args.BasicProperties.ReplyTo;
            _message.MessageId = args.BasicProperties.MessageId;
            _message.CorrelationId = args.BasicProperties.CorrelationId;
            _message.ContentType = args.BasicProperties.ContentType;
            _message.ContentEncoding = args.BasicProperties.ContentEncoding;

            try
            {
                _next?.Process(in _message);

                _next?.Synchronize();

                consumer.Model.BasicAck(args.DeliveryTag, false);

                Interlocked.Increment(ref _consumed);
            }
            catch (Exception error)
            {
                try
                {
                    //consumer.Model.BasicNack(args.DeliveryTag, false, true);
                    consumer.Model.BasicCancel(_consumerTag);
                }
                finally
                {
                    _manager.UpdatePipelineStatus(Pipeline, error.Message);
                }
            }
        }

        private void DisposeConsumer()
        {
            _message = null;
            _consumerTag = null;

            if (_consumer is not null)
            {
                _consumer.Received -= ProcessMessage;
                _consumer.Model = null;
                _consumer = null;
            }

            try { _channel?.Dispose(); }
            finally { _channel = null; }

            try { _connection?.Dispose(); }
            finally { _connection = null; }
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
        protected override void _Dispose()
        {
            if (CanDispose)
            {
                DisposeHeartbeat();

                DisposeConsumer();

                _ = Interlocked.Exchange(ref _state, STATE_IS_IDLE);
            }
            else if (Interlocked.CompareExchange(ref _state, STATE_AUTORESET, STATE_AUTORESET) == STATE_AUTORESET)
            {
                Thread.Sleep(333); _Dispose();
            }
        }
    }
}