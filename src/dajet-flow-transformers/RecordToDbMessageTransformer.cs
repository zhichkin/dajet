using DaJet.Json;
using System.Data;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Flow.Transformers
{
    [PipelineBlock] public sealed class RecordToDbMessageTransformer : TransformerBlock<IDataRecord, DbMessage>
    {
        private readonly DbMessage _buffer = new();
        private static readonly DataRecordJsonConverter _converter = new();
        private static readonly JsonWriterOptions JsonOptions = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        [Option] public string Sender { get; set; } = string.Empty;
        [Option] public string MessageType { get; set; } = string.Empty;
        public RecordToDbMessageTransformer() { }
        protected override void _Transform(in IDataRecord input, out DbMessage output)
        {
            using (MemoryStream memory = new())
            {
                using (Utf8JsonWriter writer = new(memory, JsonOptions))
                {
                    _converter.Write(writer, input, null);

                    writer.Flush();

                    _buffer.Body = Encoding.UTF8.GetString(memory.ToArray());
                }
            }

            _buffer.Uuid = Guid.NewGuid();
            _buffer.Number = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            _buffer.TimeStamp = DateTime.Now;

            _buffer.Sender = Sender;
            _buffer.Type = MessageType;

            output = _buffer;
        }
        protected override void _Synchronize() { _Dispose(); }
        protected override void _Dispose()
        {
            _buffer.Type = null;
            _buffer.Body = null;
        }
    }
}