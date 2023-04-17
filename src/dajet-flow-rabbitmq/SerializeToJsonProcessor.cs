using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Flow.RabbitMQ
{
    [PipelineBlock]
    public sealed class SerializeToJsonProcessor : ProcessorBlock<Dictionary<string, object>>
    {
        private readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        [Option] public string SenderName { get; set; } = string.Empty;
        [Option] public string MessageType { get; set; } = string.Empty;
        protected override void _Process(in Dictionary<string, object> input)
        {
            string messageBody = JsonSerializer.Serialize(input, _options);

            input.Add("AppId", SenderName);
            input.Add("Type", MessageType);
            input.Add("Body", messageBody);
        }
    }
}