using DaJet.Data;
using System.Data;
using System.Reflection;

namespace DaJet.Flow.RabbitMQ
{
    [PipelineBlock]
    public sealed class MessageToRecordTransformer : TransformerBlock<Message, IDataRecord>
    {
        private readonly DataRecord _record = new(); // buffer
        protected override void _Transform(in Message input, out IDataRecord output)
        {
            throw new NotImplementedException(nameof(MessageToRecordTransformer));

            foreach (PropertyInfo property in typeof(Message).GetProperties())
            {
                _record.SetValue(property.Name, property.GetValue(input)); // transform input to output
            }

            output = _record; // return reference to buffer
        }
        protected override void _Dispose()
        {
            _record.Clear();
        }
    }
}