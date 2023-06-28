using System.Threading.Channels;

namespace DaJet.Flow
{
    public abstract class AsyncProcessorBlock<TInput> : Configurable, IInputBlock<TInput>, IOutputBlock<TInput>
    {
        protected IInputBlock<TInput> _next;
        public void LinkTo(in IInputBlock<TInput> next) { _next = next; }
        protected abstract void _Process(in TInput input);

        private readonly Channel<TInput> _channel = Channel.CreateUnbounded<TInput>(new UnboundedChannelOptions()
        {
            SingleWriter = false,
            SingleReader = false,
            AllowSynchronousContinuations = true
        });
        private readonly ManualResetEventSlim _lock = new(true);
        [Option] public int MaxDop { get; set; } = Environment.ProcessorCount;
        public void BeginProcessing()
        {
            _lock.Wait();
        }
        public void Process(in TInput input)
        {
            if (!_channel.Writer.TryWrite(input))
            {
                throw new InvalidOperationException("Processor might be disposed.");
            }
        }
        public void Synchronize()
        {
            _lock.Reset(); // lock processor

            //TODO: handle synchronous block
            //if (_next is not AsyncProcessorBlock<TInput> next)
            //{
            //    //_next?.Synchronize();
            //    //_lock.Set(); // unlock processor
            //    //return;
            //}

            AsyncProcessorBlock<TInput> next = _next as AsyncProcessorBlock<TInput>;

            next?.BeginProcessing();

            _Synchronise();

            next?.Synchronize();

            _lock.Set(); // unlock processor
        }
        private void _Synchronise()
        {
            if (MaxDop == 1)
            {
                ProcessSynchronously();
            }
            else
            {
                Task[] tasks = new Task[MaxDop];
                for (int i = 0; i < MaxDop; i++)
                {
                    tasks[i] = Task.Run(ProcessAsync);
                }
                Task.WaitAll(tasks);
            }
        }
        private void ProcessSynchronously()
        {
            while (_channel.Reader.TryRead(out TInput input))
            {
                _Process(in input);
            }
        }
        private ValueTask ProcessAsync()
        {
            ProcessSynchronously();
            return ValueTask.CompletedTask;
        }
        public void Dispose()
        {
            //if (_channel.Writer.TryComplete())
            //{
            //    _channel.Reader.Completion.ContinueWith((task) =>
            //    {
            //        //TODO: dispose local resources ???
            //    });
            //}

            //TODO: _lock.Dispose(); ???
            
            _next?.Dispose();
        }
    }
}