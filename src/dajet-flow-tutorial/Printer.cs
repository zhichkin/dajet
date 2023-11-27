namespace DaJet.Flow.Tutorial
{
    public sealed class Printer : IInputBlock<Message>
    {
        public void Process(in Message input)
        {
            FileLogger.Default.Write($"Message: {input.Text}");
        }
        public void Synchronize()
        {
            
        }
        public void Dispose()
        {
            
        }
    }
}