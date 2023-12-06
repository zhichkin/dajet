using DaJet.Data;
using System.Reflection;

namespace DaJet.Flow.RabbitMQ
{
    public sealed class RecordToMessageTransformer : TransformerBlock<DataObject, Message>
    {
        private Message _message; // in-memory buffer
        protected override void _Transform(in DataObject input, out Message output)
        {
            _message ??= new Message(); // create buffer

            PropertyInfo property;

            for (int i = 0; i < input.Count(); i++)
            {
                property = typeof(Message).GetProperty(input.GetName(i));

                if (property is not null)
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