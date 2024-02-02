using DaJet.Data;
using DaJet.Json;
using DaJet.Metadata;
using DaJet.Stream;
using System.CommandLine;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet
{
    public static class Program
    {
        private static readonly string MS_CONNECTION = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata-ms;Integrated Security=True;Encrypt=False;";
        private static readonly string PG_CONNECTION = "Host=127.0.0.1;Port=5432;Database=dajet-metadata-pg;Username=postgres;Password=postgres;";

        private static readonly JsonWriterOptions JsonOptions = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        private static readonly DataObjectJsonConverter _converter = new();
        private static readonly IMetadataProvider _context = new OneDbMetadataProvider(MS_CONNECTION);
        //private static readonly IMetadataProvider _context = new OneDbMetadataProvider(PG_CONNECTION);

        public static int Main(string[] args)
        {
            args = new string[]
            {
                "stream", "--url", "dajet://ms-demo/stream/test"
            };

            var root = new RootCommand("dajet");
            var command = new Command("stream", "Execute DaJet Stream");
            var option = new Option<string>("--url", "Script URL");
            command.Add(option);
            command.SetHandler(Stream, option);
            root.Add(command);

            return root.Invoke(args);
        }
        private static void Stream(string url)
        {
            StreamProcessor.Process(in url);
        }
        private static void WriteResultToFile(in DataObject input)
        {
            using (MemoryStream memory = new())
            {
                using (Utf8JsonWriter writer = new(memory, JsonOptions))
                {
                    _converter.Write(writer, input, null);

                    writer.Flush();

                    string json = Encoding.UTF8.GetString(memory.ToArray());

                    Console.WriteLine(json);

                    FileLogger.Default.Write(json);
                }
            }
        }
    }
}