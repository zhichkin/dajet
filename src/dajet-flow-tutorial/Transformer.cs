using DaJet.Data;
using DaJet.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Flow.Tutorial
{
    public sealed class Transformer : TransformerBlock<DataObject, DataObject>
    {
        private static readonly JsonWriterOptions JsonOptions = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private static readonly DataObjectJsonConverter _converter = new();

        private readonly TransformerOptions _options;
        public Transformer(TransformerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }
        protected override void _Transform(in DataObject input, out DataObject output)
        {
            DataObject message = new();

            if (string.IsNullOrWhiteSpace(_options.MessageType))
            {
                message.SetValue("ТипСообщения", input.GetName());
            }
            else
            {
                message.SetValue("ТипСообщения", _options.MessageType);
            }

            using (MemoryStream memory = new())
            {
                using (Utf8JsonWriter writer = new(memory, JsonOptions))
                {
                    _converter.Write(writer, input, null);

                    writer.Flush();

                    string json = Encoding.UTF8.GetString(memory.ToArray());

                    message.SetValue("ТелоСообщения", json);
                }
            }
            
            output = message;

            //NOTE: dynamic msg = message;
            //NOTE: msg.ТипСообщения = "test dynamic";
            //NOTE: msg.ТелоСообщения = msg.ТипСообщения;
        }
    }
}