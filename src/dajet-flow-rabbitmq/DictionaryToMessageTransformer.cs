using System.Reflection;

namespace DaJet.Flow.RabbitMQ
{
    [PipelineBlock]
    public sealed class DictionaryToMessageTransformer : TransformerBlock<Dictionary<string, object>, Message>
    {
        private Message _message; // buffer
        protected override void _Transform(in Dictionary<string, object> input, out Message output)
        {
            if (_message is null)
            {
                _message = new(); // create buffer
            }

            PropertyInfo property;

            foreach (var item in input)
            {
                property = typeof(Message).GetProperty(item.Key);

                property?.SetValue(_message, item.Value);
            }

            output = _message; // return reference to buffer
        }
        protected override void _Dispose()
        {
            _message = null; // clear buffer
        }
    }
}