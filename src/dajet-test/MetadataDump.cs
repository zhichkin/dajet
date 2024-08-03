using DaJet.Data;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Metadata.Parsers;

namespace DaJet.Metadata.Test
{
    [TestClass] public class MetadataDump
    {
        private static readonly string MS_CONNECTION = "Data Source=ZHICHKIN;Initial Catalog=dajet-exchange;Integrated Security=True;Encrypt=False;";
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
        [TestMethod] public void DumpConfigFile()
        {
            IMetadataProvider metadata = new OneDbMetadataProvider(MS_CONNECTION);

            MetadataObject entity = metadata.GetMetadataObject("РегистрСведений.Тестовый");

            ConfigFileParser parser = new();
            ConfigFileWriter writer = new();

            using (ConfigFileReader reader = new(metadata.DatabaseProvider, metadata.ConnectionString, ConfigTables.Config, entity.Uuid))
            {
                ConfigObject config = parser.Parse(in reader);
                writer.Write(config, @"C:\temp\1c-dump\РегистрСведений.Тестовый.dump.txt");
            }

            Console.WriteLine("Done");
        }
    }
}