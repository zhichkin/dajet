using DaJet.Data;
using DaJet.Json;
using System.Data;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Flow.Tutorial
{
    public sealed class Transformer : TransformerBlock<IDataRecord, IDataRecord>
    {
        private static readonly JsonWriterOptions JsonOptions = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private static readonly DataRecordJsonConverter _converter = new();

        private readonly TransformerOptions _options;
        public Transformer(TransformerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }
        protected override void _Transform(in IDataRecord input, out IDataRecord output)
        {
            DataRecord record = new();

            record.SetValue("ТипСообщения", _options.MessageType);

            using (MemoryStream memory = new())
            {
                using (Utf8JsonWriter writer = new(memory, JsonOptions))
                {
                    _converter.Write(writer, input, null);

                    writer.Flush();

                    string json = Encoding.UTF8.GetString(memory.ToArray());

                    record.SetValue("ТелоСообщения", json);
                }
            }

            output = record;
        }
    }
}