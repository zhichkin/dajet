﻿using DaJet.Data;
using DaJet.Json;
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
        private static readonly JsonWriterOptions JsonOptions = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private static readonly DataObjectJsonConverter _converter = new();

        public static int Main(string[] args)
        {
            //args = new string[]
            //{
            //    "stream", "--file",

            //    //"./test/20-http-post.sql"
            //    //"./test/21-http-query.sql"

            //    //"./stream/03-ms-exchange-kafka-producer.sql"
            //    //"./stream/04-kafka-consumer-pg-register.sql"

            //    //"./stream/10-simple-amqp-produce.sql"
            //    //"./stream/11-ms-amqp-produce.sql"
            //    //"./stream/12-amqp-pg-consume.sql"
            //    //"./stream/13-amqp-amqp-shovel.sql"

            //    //"./test/11-ms-pg-exchange-consume-maxdop.sql"
            //    //"./test/07-ms-pg-catalog-paging-maxdop.sql"
            //};

            var root = new RootCommand("dajet");
            var command = new Command("stream", "Execute DaJet Stream");
            var option1 = new Option<string>("--url", "Script URL");
            var option2 = new Option<string>("--file", "Script path");
            command.AddOption(option1);
            command.AddOption(option2);
            command.SetHandler(Stream, option1, option2);
            root.Add(command);

            return root.Invoke(args);
        }
        private static void Stream(string url, string file)
        {
            if (string.IsNullOrEmpty(file))
            {
                StreamUri(url);
            }
            else
            {
                StreamFile(file);
            }
        }
        private static void StreamUri(string url)
        {
            throw new NotImplementedException();

            //Console.WriteLine($"Execute script from URL: {url}");

            //Uri uri = new(url);

            //StreamProcessor.Process(in uri);
        }
        private static void StreamFile(string filePath)
        {
            FileInfo file = new(filePath);

            Console.WriteLine($"Execute script from file: {file.FullName}");

            string script;

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                script = reader.ReadToEnd();
            }

            StreamManager.Process(in script);
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