using DaJet.Flow;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text;
using System.Timers;
using System.Web;

namespace DaJet.Exchange.RabbitMQ
{
    [PipelineBlock] public sealed class Consumer : SourceBlock<OneDbMessage>
    {
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
        private OneDbMessage _message;
        private int _consumed = 0;
        private string _consumerTag;
        private readonly IPipelineManager _manager;
        [ActivatorUtilitiesConstructor]
        public Consumer(IPipelineManager manager)
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
            _manager.UpdatePipelineFinishTime(Pipeline, DateTime.Now);
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

                ManualResetEvent cancellation = new(false);

                if (Interlocked.CompareExchange(ref _cancellation, cancellation, null) is not null)
                {
                    cancellation.Dispose();
                }

                EnsureConsumerIsActive(this, null);

                _heartbeat.Start();
                _cancellation.WaitOne();
            }
        }

        private void ProcessMessage(object sender, BasicDeliverEventArgs args)
        {
            if (sender is not EventingBasicConsumer consumer) { return; }

            _message ??= new OneDbMessage();
            _message.Sender = args.BasicProperties.AppId;
            _message.TypeName = args.BasicProperties.Type;
            _message.Payload = Encoding.UTF8.GetString(args.Body.Span);
            //_message.Subscribers.Clear();

            if (long.TryParse(args.BasicProperties.MessageId, out long sequence))
            {
                _message.Sequence = sequence;
            }
            else
            {
                _message.Sequence = 0L;
            }

            try
            {
                _next.Process(in _message);

                _next.Synchronize();

                consumer.Model.BasicAck(args.DeliveryTag, false);

                Interlocked.Increment(ref _consumed);
            }
            catch (Exception error)
            {
                NackMessage(in consumer, in args, in error);
            }
        }
        private void NackMessage(in EventingBasicConsumer consumer, in BasicDeliverEventArgs args, in Exception error)
        {
            _manager.UpdatePipelineStatus(Pipeline, error.Message);
            _manager.UpdatePipelineFinishTime(Pipeline, DateTime.Now);

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

            _message = null;
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
                _cancellation?.Set();
                _cancellation?.Dispose();
            }
            finally
            {
                _cancellation = null;
            }
        }
        protected override void _Dispose()
        {
            if (CanDispose)
            {
                DisposeHeartbeat();

                DisposeConsumer();

                SignalCancellation();

                _ = Interlocked.Exchange(ref _state, STATE_IS_IDLE);
            }
            else if (Interlocked.CompareExchange(ref _state, STATE_AUTORESET, STATE_AUTORESET) == STATE_AUTORESET)
            {
                Thread.Sleep(10); _Dispose();
            }
        }
    }
}