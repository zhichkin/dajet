using DaJet.Flow;
using DaJet.Flow.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Stream
{
    [PipelineBlock] public sealed class OneDbSerializer : BufferProcessorBlock<OneDbMessage>
    {
        private static readonly JsonWriterOptions JsonOptions = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private static readonly DataRecordJsonConverter _converter = new();
        protected override void _Process(in OneDbMessage input)
        {
            using (MemoryStream memory = new())
            {
                using (Utf8JsonWriter writer = new(memory, JsonOptions))
                {
                    _converter.Write(writer, input.DataRecord, null);

                    writer.Flush();

                    input.Payload = Encoding.UTF8.GetString(memory.ToArray());
                }
            }
        }
    }
}