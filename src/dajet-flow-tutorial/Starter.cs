namespace DaJet.Flow.Tutorial
{
    public sealed class Starter : ISourceBlock, IOutputBlock<Message>
    {
        private IInputBlock<Message> _next;
        public void LinkTo(in IInputBlock<Message> next)
        {
            _next = next;
        }
        private readonly IPipeline _pipeline;
        private readonly StarterOptions _options;
        public Starter(StarterOptions options, IPipeline pipeline)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }
        public void Execute()
        {
            _pipeline.UpdateMonitorStatus("Starter is executing...");

            Thread.Sleep(TimeSpan.FromSeconds(15));
        }
        public void Dispose()
        {
            _pipeline.UpdateMonitorStatus("Starter stopped.");
        }
    }
}