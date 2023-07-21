using System.Threading.Channels;

namespace DaJet.Flow
{
    public abstract class BufferProcessorBlock<TInput> : Configurable, IInputBlock<TInput>, IOutputBlock<TInput>
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
        [Option] public int MaxDop { get; set; } = 1;
        public void Process(in TInput input)
        {
            if (!_channel.Writer.TryWrite(input))
            {
                throw new InvalidOperationException("Processor might be disposed.");
            }
        }
        public void Synchronize()
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

            _next?.Synchronize();
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

            //while (_channel.Reader.TryRead(out _))
            //{
            //    // empty channel buffer
            //}

            _next?.Dispose();
        }
    }
}