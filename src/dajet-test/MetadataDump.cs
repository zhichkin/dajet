using DaJet.Data;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Metadata.Parsers;

namespace DaJet.Metadata.Test
{
    [TestClass] public class MetadataDump
    {
        private static readonly string MS_CONNECTION = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata;Integrated Security=True;Encrypt=False;";
        private static readonly string PG_CONNECTION = "Host=127.0.0.1;Port=5432;Database=dajet-metadata;Username=postgres;Password=postgres;";
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
        [TestMethod] public void DumpDbNames()
        {
            DatabaseProvider provider = DatabaseProvider.SqlServer;

            ConfigFileParser parser = new();
            ConfigFileWriter writer = new();

            using (ConfigFileReader reader = new(provider, in MS_CONNECTION, ConfigTables.Params, ConfigFiles.DbNames))
            {
                ConfigObject config = parser.Parse(in reader);
                writer.Write(config, @"C:\temp\1c-dump\DBNames.txt");
            }

            Console.WriteLine("Done: DBNames");
        }
        [TestMethod] public void DumpDbNamesExt1()
        {
            string fileName = ConfigFiles.DbNames + "-Ext-1";
            DatabaseProvider provider = DatabaseProvider.SqlServer;

            ConfigFileParser parser = new();
            ConfigFileWriter writer = new();

            using (ConfigFileReader reader = new(provider, in MS_CONNECTION, ConfigTables.Params, fileName))
            {
                ConfigObject config = parser.Parse(in reader);
                writer.Write(config, @"C:\temp\1c-dump\DBNames-Ext-1.txt");
            }

            Console.WriteLine("MS: DBNames-Ext-1");
        }
        [TestMethod] public void DumpPgDbNamesExt1()
        {
            string fileName = ConfigFiles.DbNames + "-Ext-1";
            DatabaseProvider provider = DatabaseProvider.PostgreSql;

            ConfigFileParser parser = new();
            ConfigFileWriter writer = new();

            using (ConfigFileReader reader = new(provider, in PG_CONNECTION, ConfigTables.Params, fileName))
            {
                ConfigObject config = parser.Parse(in reader);
                writer.Write(config, @"C:\temp\1c-dump\PG-DBNames-Ext-1.txt");
            }

            Console.WriteLine("PG: DBNames-Ext-1");
        }
        [TestMethod] public void DumpDbNamesExtUuid()
        {
            string fileName = "DBNames-Ext-575842b9-1d09-11f0-9d46-3c64cfca4840";
            DatabaseProvider provider = DatabaseProvider.SqlServer;

            ConfigFileParser parser = new();
            ConfigFileWriter writer = new();

            using (ConfigFileReader reader = new(provider, in MS_CONNECTION, ConfigTables.Params, fileName))
            {
                ConfigObject config = parser.Parse(in reader);
                writer.Write(config, @"C:\temp\1c-dump\DBNames-Ext-Uuid.txt");
            }

            Console.WriteLine("Done: DBNames-Ext-Uuid");
        }
        [TestMethod] public void DumpBusinessProcessRoutingMap()
        {
            string fileName = "35608879-5406-4b60-9386-cd0a6ad2446e.7";
            DatabaseProvider provider = DatabaseProvider.SqlServer;

            ConfigFileParser parser = new();
            ConfigFileWriter writer = new();

            using (ConfigFileReader reader = new(provider, in MS_CONNECTION, ConfigTables.Config, fileName))
            {
                ConfigObject config = parser.Parse(in reader);
                writer.Write(config, @"C:\temp\1c-dump\business-process-routing-map.txt");
            }

            Console.WriteLine("Done: business-process-routing-map");
        }
        [TestMethod] public void DumpConfigFileByUuid()
        {
            // Задача 268725a6-36ee-442e-8ae2-dc029a26cd12
            // Процесс 35608879-5406-4b60-9386-cd0a6ad2446e
            // Карта маршрута 35608879-5406-4b60-9386-cd0a6ad2446e.7
            Guid file_uuid = new("35608879-5406-4b60-9386-cd0a6ad2446e");
            DatabaseProvider provider = DatabaseProvider.SqlServer;

            ConfigFileParser parser = new();
            ConfigFileWriter writer = new();

            using (ConfigFileReader reader = new(provider, in MS_CONNECTION, ConfigTables.Config, file_uuid))
            {
                ConfigObject config = parser.Parse(in reader);
                writer.Write(config, @"C:\temp\1c-dump\business_process_file_dump.txt");
            }

            Console.WriteLine("Done");
        }
        [TestMethod] public void DumpConfigFileByName()
        {
            IMetadataProvider metadata = new OneDbMetadataProvider(MS_CONNECTION);

            //string metadataName = "Константа.Булево";
            //string dumpFilePath = "C:\\temp\\1c-dump\\Константа.Булево.dump.txt";
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
        [TestMethod] public void DumpExtensionFileByUuid()
        {
            string file_name = "d8822b02cb494379dc95ab9916f82acafd06f62e";
            DatabaseProvider provider = DatabaseProvider.SqlServer;

            ConfigFileParser parser = new();
            ConfigFileWriter writer = new();

            using (ConfigFileReader reader = new(provider, in MS_CONNECTION, ConfigTables.ConfigCAS, file_name))
            {
                ConfigObject config = parser.Parse(in reader);
                writer.Write(config, @"C:\temp\1c-dump\constant_file_dump_borrowed_ext.txt");
            }

            Console.WriteLine("Done");
        }
    }
}