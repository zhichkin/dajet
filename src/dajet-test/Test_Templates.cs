using DaJet.Data;
using DaJet.Data.Provider;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO.Compression;
using System.Text;

namespace DaJet.Templates.Test
{
    [TestClass] public class Test_Templates
    {
        private const string MS_ERP_INFOBASE = "Data Source=ZHICHKIN;Initial Catalog=cerberus;Integrated Security=True;Encrypt=False;";
        private const string PG_CONNECTION_STRING = "Host=127.0.0.1;Port=5432;Database=dajet-metadata-pg;Username=postgres;Password=postgres;";
        private const string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata-ms;Integrated Security=True;Encrypt=False;";

        private readonly MetadataCache _cache;
        private readonly MetadataService _service;
        
        public Test_Templates()
        {
            _service = new MetadataService();

            _service.Add(new InfoBaseOptions()
            {
                Key = "test",
                ConnectionString = MS_CONNECTION_STRING, // PG_CONNECTION_STRING
                DatabaseProvider = DatabaseProvider.SqlServer // DatabaseProvider.PostgreSql
            });

            if (!_service.TryGetMetadataCache("test", out _cache, out string error))
            {
                Console.WriteLine(error);
                return;
            }
        }
        [TestMethod] public void Dump_Template()
        {
            string metadataName = "ПланОбмена.ПланОбмена1";
            string outputFile = $"C:\\temp\\1c-dump\\{metadataName}.txt";

            string connectionString = _cache.ConnectionString;
            DatabaseProvider provider = _cache.DatabaseProvider;

            Publication entity = _cache.GetMetadataObject<Publication>(metadataName);

            using (ConfigFileReader reader = new(provider, connectionString, ConfigTables.Config, entity.Uuid))
            {
                ConfigObject configObject = new ConfigFileParser().Parse(reader);

                new ConfigFileWriter().Write(configObject, outputFile);
            }

            Console.WriteLine();
            Console.WriteLine($"{_cache.InfoBase.Name} [{_cache.InfoBase.AppConfigVersion}]");
            Console.WriteLine($"{metadataName} [{entity.Uuid}]");
            Console.WriteLine();

            foreach (Template template in entity.Templates)
            {
                if (template.Name == "ТекстовыйМакет")
                {
                    Console.WriteLine($"{template.Name} ({template.Alias}) [{template.Type}] {template.Comment}");

                    string fileName = template.GetFileName();

                    using (StreamReader reader = ConfigFileReader.Create(in connectionString, ConfigTables.Config, in fileName))
                    {
                        fileName = $"{entity}.{template}.xml";

                        using (StreamWriter writer = new($"C:\\temp\\1c-dump\\{fileName}", false, Encoding.UTF8))
                        {
                            writer.Write(reader.ReadToEnd());
                        }
                    }

                    break;
                }
            }
        }
    }
}