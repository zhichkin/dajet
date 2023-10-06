using DaJet.Json;
using System.Data;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Flow.RabbitMQ
{
    [PipelineBlock]
    public sealed class MessageToRecordTransformer : TransformerBlock<Message, IDataRecord>
    {
        private static readonly DataRecordJsonConverter _converter = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        protected override void _Transform(in Message input, out IDataRecord output)
        {
            Utf8JsonReader reader = new(input.Payload.Data.Span, true, default);

            output = _converter.Read(ref reader, typeof(IDataRecord), JsonOptions);
        }
    }
}