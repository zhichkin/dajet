using DaJet.Flow.Json;
using System.Data;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Flow
{
    [PipelineBlock] public sealed class PayloadToRecordTransformer : TransformerBlock<Payload, IDataRecord>
    {
        private static readonly DataRecordJsonConverter _converter = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        protected override void _Transform(in Payload input, out IDataRecord output)
        {
            Utf8JsonReader reader = new(input.Data.Span, true, default);

            output = _converter.Read(ref reader, typeof(IDataRecord), JsonOptions);
        }
    }
}