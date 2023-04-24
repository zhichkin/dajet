using System.Data;
using System.Reflection;

namespace DaJet.Flow.RabbitMQ
{
    [PipelineBlock]
    public sealed class RecordToMessageTransformer : TransformerBlock<IDataRecord, Message>
    {
        private Message _message; // buffer
        protected override void _Transform(in IDataRecord input, out Message output)
        {
            _message ??= new Message(); // create buffer

            PropertyInfo property;

            for (int i = 0; i < input.FieldCount; i++)
            {
                property = typeof(Message).GetProperty(input.GetName(i));

                if (property is null) { continue; }

                if (input.IsDBNull(i))
                {
                    property.SetValue(_message, null);
                }
                else
                {
                    property.SetValue(_message, input.GetValue(i));
                }
            }

            output = _message; // return reference to buffer
        }
        protected override void _Dispose()
        {
            _message = null; // clear buffer
        }
    }
}