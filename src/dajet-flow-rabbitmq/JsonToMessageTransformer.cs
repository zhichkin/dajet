namespace DaJet.Flow.RabbitMQ
{
    [PipelineBlock]
    public sealed class JsonToMessageTransformer : TransformerBlock<ReadOnlyMemory<byte>, Message>
    {
        private Message _message; // buffer
        protected override void _Transform(in ReadOnlyMemory<byte> input, out Message output)
        {
            _message ??= new Message(); // create buffer

            _message.MessageBody = input;

            output = _message; // return reference to buffer
        }
        protected override void _Dispose()
        {
            _message = null; // clear buffer
        }
    }
}