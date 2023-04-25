using DaJet.Flow.Json;
using Microsoft.IO;
using System.Data;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Flow
{
    [PipelineBlock] public sealed class RecordToJsonTransformer : TransformerBlock<IDataRecord, Payload>
    {
        private static readonly DataRecordJsonConverter _converter = new();
        private static readonly JsonWriterOptions JsonOptions = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private MemoryStream _stream;
        private readonly Action _callback;
        private readonly RecyclableMemoryStreamManager _memory;
        public RecordToJsonTransformer(RecyclableMemoryStreamManager manager)
        {
            _callback = _Dispose;

            _memory = manager ?? throw new ArgumentNullException(nameof(manager));
        }
        protected override void _Transform(in IDataRecord input, out Payload output)
        {
            _stream = _memory.GetStream(nameof(RecordToJsonTransformer));

            using (Utf8JsonWriter writer = new(_stream, JsonOptions))
            {
                _converter.Write(writer, input, null);

                writer.Flush();

                ReadOnlyMemory<byte> data = new(_stream.GetBuffer(), 0, (int)_stream.Length);

                output = new Payload(data, _callback);
            }
        }
        protected override void _Synchronize() { _Dispose(); }
        protected override void _Dispose()
        {
            try
            {
                _stream?.Dispose();
            }
            finally
            {
                _stream = null;
            }
        }
    }
}