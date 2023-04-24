namespace DaJet.Flow.RabbitMQ
{
    [PipelineBlock]
    public sealed class PayloadToMessageTransformer : TransformerBlock<Payload, Message>
    {
        private Message _message; // buffer
        protected override void _Transform(in Payload input, out Message output)
        {
            _message ??= new Message(); // create buffer

            _message.Payload = input;

            output = _message; // return reference to buffer
        }
        protected override void _Dispose()
        {
            _message = null; // clear buffer
        }
    }
}