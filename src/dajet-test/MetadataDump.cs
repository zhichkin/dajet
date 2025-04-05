using DaJet.Data;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Metadata.Parsers;

namespace DaJet.Metadata.Test
{
    [TestClass] public class MetadataDump
    {
        private static readonly string MS_CONNECTION = "Data Source=ZHICHKIN;Initial Catalog=unf;Integrated Security=True;Encrypt=False;";
        private static readonly string PG_CONNECTION = "Host=127.0.0.1;Port=5432;Database=dajet-exchange;Username=postgres;Password=postgres;";
        static MetadataDump()
        {
            
        }
        [TestMethod] public void DumpRootFile()
        {
            Guid root_file_uuid;
            DatabaseProvider provider = DatabaseProvider.SqlServer;

            ConfigFileParser parser = new();
            ConfigFileWriter writer = new();

            using (ConfigFileReader reader = new(provider, in MS_CONNECTION, ConfigTables.Config, ConfigFiles.Root))
            {
                root_file_uuid = new RootFileParser().Parse(in reader);
            }

            using (ConfigFileReader reader = new(provider, in MS_CONNECTION, ConfigTables.Config, root_file_uuid))
            {
                ConfigObject config = parser.Parse(in reader);
                writer.Write(config, @"C:\temp\1c-dump\config.txt");
            }

            Console.WriteLine("Done");
        }
        [TestMethod] public void DumpConfigFileByUuid()
        {
            Guid file_uuid = new("738577f7-2f37-4c05-a9e9-70f9d835939f");
            DatabaseProvider provider = DatabaseProvider.SqlServer;

            ConfigFileParser parser = new();
            ConfigFileWriter writer = new();

            using (ConfigFileReader reader = new(provider, in MS_CONNECTION, ConfigTables.Config, file_uuid))
            {
                ConfigObject config = parser.Parse(in reader);
                writer.Write(config, @"C:\temp\1c-dump\file_dump.txt");
            }

            Console.WriteLine("Done");
        }
        [TestMethod] public void DumpConfigFileByName()
        {
            IMetadataProvider metadata = new OneDbMetadataProvider(MS_CONNECTION);

            string metadataName = "Справочник.Номенклатура";
            string dumpFilePath = "C:\\temp\\1c-dump\\Справочник.Номенклатура.dump.txt";
            //string metadataName = "ПланСчетов.ПланСчетов1";
            //string dumpFilePath = "C:\\temp\\1c-dump\\ПланСчетов.ПланСчетов1.dump.txt";
            //string metadataName = "РегистрБухгалтерии.РегистрБухгалтерии1";
            //string dumpFilePath = "C:\\temp\\1c-dump\\РегистрБухгалтерии.РегистрБухгалтерии1.dump.txt";

            MetadataObject entity = metadata.GetMetadataObject(metadataName);

            if (entity is null)
            {
                Console.WriteLine($"Not found: {metadataName}"); return;
            }

            ConfigFileParser parser = new();
            ConfigFileWriter writer = new();

            using (ConfigFileReader reader = new(metadata.DatabaseProvider, metadata.ConnectionString, ConfigTables.Config, entity.Uuid))
            {
                ConfigObject config = parser.Parse(in reader);
                writer.Write(config, dumpFilePath);
            }

            Console.WriteLine($"Metadata name: {metadataName}");
            Console.WriteLine($"Dump file path: {dumpFilePath}");
            Console.WriteLine("Success");
        }
    }
}