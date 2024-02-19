using DaJet.Scripting.Model;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Timers;
using System.Web;

namespace DaJet.Stream.RabbitMQ
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
        private Message _message; // data buffer
        private int _consumed = 0; // in-memory counter
        private string _consumerTag;
        private readonly IPipeline _pipeline; // in-memory online monitoring
        private readonly ConsumerOptions _options;
        public Consumer(in ConsumeStatement options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            
            Configure();
        }
        public void Synchronize() { /* do nothing */ }
        public void LinkTo(in IProcessor next) { _next = next; }
        private void Configure()
        {
            if (_options.Heartbeat < 10) { _options.Heartbeat = 10; }

            if (string.IsNullOrWhiteSpace(_options.Source)) { return; }

            Uri uri = new(_options.Source);

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

        #region "CONFIGURATION OPTIONS"
        private string HostName { get; set; } = "localhost";
        private int HostPort { get; set; } = 5672;
        private string VirtualHost { get; set; } = "/";
        private string UserName { get; set; } = "guest";
        private string Password { get; set; } = "guest";
        #endregion

        private void UpdatePipelineStatus()
        {
            int consumed = Interlocked.Exchange(ref _consumed, 0);

            _pipeline.UpdateMonitorStatus($"Consumed {consumed} messages in {_options.Heartbeat} seconds.");
            _pipeline.UpdateMonitorFinishTime(DateTime.Now);
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
            _consumerTag = _channel.BasicConsume(_options.Queue, false, _consumer);
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

                    _pipeline.UpdateMonitorStatus(error.Message);
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
                System.Timers.Timer timer = new();

                if (Interlocked.CompareExchange(ref _heartbeat, timer, null) is not null)
                {
                    timer.Dispose();
                }
                else
                {
                    _heartbeat.AutoReset = true;
                    _heartbeat.Elapsed += EnsureConsumerIsActive;
                    _heartbeat.Interval = TimeSpan.FromSeconds(_options.Heartbeat).TotalMilliseconds;
                }

                ManualResetEvent cancellation = new(false);

                if (Interlocked.CompareExchange(ref _cancellation, cancellation, null) is not null)
                {
                    cancellation.Dispose();
                }

                EnsureConsumerIsActive(this, null);

                _heartbeat.Start();
                _cancellation.WaitOne(); // instead of Task.Delay(TimeSpan)
            }
        }
        
        private void ProcessMessage(object sender, BasicDeliverEventArgs args)
        {
            if (sender is not EventingBasicConsumer consumer) { return; }

            _message ??= new Message();
            _message.Payload = args.Body;
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
                NackMessage(in consumer, in args, in error);
            }
        }
        private void NackMessage(in EventingBasicConsumer consumer, in BasicDeliverEventArgs args, in Exception error)
        {
            _pipeline.UpdateMonitorStatus(error.Message);
            _pipeline.UpdateMonitorFinishTime(DateTime.Now);

            if (_cancellation is null)
            {
                return; // Consumer is most likely already disposed
            }

            bool signaled = _cancellation.WaitOne(TimeSpan.FromSeconds(_options.Heartbeat));

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
        public void Dispose()
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
                Thread.Sleep(10); Dispose();
            }

            _next?.Dispose();
        }
    }
}