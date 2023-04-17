using System.Reflection;

namespace DaJet.Flow.RabbitMQ
{
    [PipelineBlock]
    public sealed class MessageToDictionaryTransformer : TransformerBlock<Message, Dictionary<string, object>>
    {
        private readonly Dictionary<string, object> _buffer = new(); // buffer
        public MessageToDictionaryTransformer()
        {
            foreach (PropertyInfo property in typeof(Message).GetProperties())
            {
                if (property.Name != "Headers")
                {
                    _buffer.Add(property.Name, null); // prepare buffer for work
                }
            }
        }
        protected override void _Transform(in Message input, out Dictionary<string, object> output)
        {
            foreach (PropertyInfo property in typeof(Message).GetProperties())
            {
                if (_buffer.ContainsKey(property.Name))
                {
                    _buffer[property.Name] = property.GetValue(input); // transform input to output
                }
            }

            output = _buffer; // return reference to buffer
        }
        protected override void _Dispose()
        {
            _buffer.Clear();
        }
    }
}