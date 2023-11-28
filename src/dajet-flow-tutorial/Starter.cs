namespace DaJet.Flow.Tutorial
{
    public sealed class Starter : ISourceBlock, IOutputBlock<Message>
    {
        private IInputBlock<Message> _next;
        public void LinkTo(in IInputBlock<Message> next)
        {
            _next = next;
        }
        private readonly StarterOptions _options;
        public Starter(StarterOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }
        public void Execute()
        {
            FileLogger.Default.Write("Starter executing...");

            FileLogger.Default.Write($"Starter: {_options.Greeting}, {_options.Name} !");
        }
        public void Dispose()
        {
            FileLogger.Default.Write("Starter stopped.");
        }
    }
}