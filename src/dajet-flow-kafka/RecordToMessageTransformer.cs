using Confluent.Kafka;
using System.Data;

namespace DaJet.Flow.Kafka
{
    // https://protobuf.dev/getting-started/csharptutorial/
    [PipelineBlock] public sealed class RecordToMessageTransformer : TransformerBlock<IDataRecord, Message<byte[], byte[]>>
    {
        private readonly Message<byte[], byte[]> _output = new();
        protected override void _Configure()
        {

        }
        protected override void _Transform(in IDataRecord input, out Message<byte[], byte[]> output)
        {
            DbMessage message = new()
            {
                Type = input.GetString(input.GetOrdinal("ТипСообщения")),
                Body = input.GetString(input.GetOrdinal("ТелоСообщения")),
                TimeStamp = DateTime.UtcNow,
                Number = DateTime.UtcNow.Ticks % TimeSpan.TicksPerSecond
            };

            //_output.Key = Guid.NewGuid().ToByteArray();
            //_output.Value = message.ToByteArray();

            output = _output;
        }
        protected override void _Dispose()
        {
            _output.Key = null;
            _output.Value = null;
        }
    }
}