namespace DaJet.Flow.Tutorial
{
    public sealed class Starter : ISourceBlock, IOutputBlock<Message>
    {
        private IInputBlock<Message> _next;
        public void LinkTo(in IInputBlock<Message> next)
        {
            _next = next;
        }
        public void Execute()
        {
            for (int i = 0; i < 3; i++)
            {
                if (_next is not null)
                {
                    Message message = new()
                    {
                        Text = i.ToString()
                    };

                    _next.Process(message);
                }
            }
        }
        public void Dispose()
        {
            FileLogger.Default.Write("Plugin is stopped.");
        }
    }
}