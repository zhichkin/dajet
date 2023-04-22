using DaJet.Flow.Json;
using Microsoft.IO;
using System.Buffers;
using System.Data;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Flow.RabbitMQ
{
    [PipelineBlock]
    public sealed class RecordToJsonTransformer : TransformerBlock<IDataRecord, ReadOnlyMemory<byte>>
    {
        private static readonly DataRecordJsonConverter _converter = new();
        private static readonly JsonWriterOptions JsonOptions = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private readonly RecyclableMemoryStreamManager MemoryPool;
        private byte[] _buffer;
        public RecordToJsonTransformer(RecyclableMemoryStreamManager memoryPool)
        {
            MemoryPool = memoryPool ?? throw new ArgumentNullException(nameof(memoryPool));
        }
        protected override void _Transform(in IDataRecord input, out ReadOnlyMemory<byte> output)
        {
            int length;

            using (MemoryStream stream = MemoryPool.GetStream())
            {
                using (Utf8JsonWriter writer = new(stream, JsonOptions))
                {
                    _converter.Write(writer, input, null);

                    writer.Flush();

                    length = (int)writer.BytesCommitted;

                    if (_buffer is not null)
                    {
                        ArrayPool<byte>.Shared.Return(_buffer);
                    }

                    _buffer = ArrayPool<byte>.Shared.Rent(length);

                    Buffer.BlockCopy(stream.GetBuffer(), 0, _buffer, 0, length);
                }
            }
            
            output = new ReadOnlyMemory<byte>(_buffer, 0, length);
        }
        protected override void _Synchronize() { _Dispose(); }
        protected override void _Dispose()
        {
            if (_buffer is not null)
            {
                ArrayPool<byte>.Shared.Return(_buffer); _buffer = null;
            }
        }
    }
}