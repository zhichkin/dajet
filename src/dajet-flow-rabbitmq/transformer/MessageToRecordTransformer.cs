using DaJet.Data;
using DaJet.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Flow.RabbitMQ
{
    public sealed class MessageToRecordTransformer : TransformerBlock<Message, DataObject>
    {
        private static readonly DataObjectJsonConverter _converter = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        protected override void _Transform(in Message input, out DataObject output)
        {
            Utf8JsonReader reader = new(input.Payload.Span, true, default);

            output = _converter.Read(ref reader, typeof(DataObject), JsonOptions);
        }
    }
}