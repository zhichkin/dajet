using DaJet.Data.Client;
using DaJet.Data;
using DaJet.Metadata;
using System.CommandLine;
using System.Text;
using DaJet.Json;
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
                "script", "--file", "./test/ms-aaa-trash.txt"
            };
            //"./test/apply.txt"
            //"./test/ms-declare-select.txt"
            //"./test/pg-context-import.txt"

            var root = new RootCommand("dajet");
            
            var command = new Command("script", "Execute DaJet script");
            var option = new Option<string>("--file", "Script file path");
            command.Add(option);
            command.SetHandler(ExecuteScript, option);

            root.Add(command);

            return root.Invoke(args);
        }
        private static void ExecuteScript(string filePath)
        {
            FileInfo file = new(filePath);

            Console.WriteLine($"Execute script: {file.FullName}");

            //ScriptEngine.Execute(in filePath);

            string script;

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                script = reader.ReadToEnd();
            }

            using (OneDbConnection connection = new(_context))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = script;
                    //command.Parameters.Add("КодВалюты", "840");
                    //command.Parameters.Add("Валюта", new Entity(60, Guid.Empty));
                    //command.Parameters.Add("Номенклатура", new Entity(50, Guid.Empty));

                    foreach (DataObject record in command.StreamReader())
                    {
                        //for (int i = 0; i < record.Count(); i++)
                        //{
                        //    string name = record.GetName(i);
                        //    object value = record.GetValue(i);
                        //}

                        WriteResultToFile(in record);
                    }
                }
            }
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