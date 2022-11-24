using DaJet.Data;
using DaJet.Data.Provider;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO.Compression;
using System.Text;

namespace DaJet.Metadata.Test
{
    [TestClass] public class Write_Config_File
    {
        private const string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata-ms;Integrated Security=True;Encrypt=False;";
        [TestMethod] public void GetMetadataItemByName()
        {
            MetadataService metadata = new();
            metadata.Add(new InfoBaseOptions()
            {
                Key = "dajet-metadata-ms",
                ConnectionString = MS_CONNECTION_STRING,
                DatabaseProvider = DatabaseProvider.SqlServer
            });

            if (!metadata.TryGetMetadataCache("dajet-metadata-ms", out MetadataCache cache, out string error))
            {
                Console.WriteLine(error); return;
            }

            var register = cache.GetMetadataObject<InformationRegister>("РегистрСведений.ТестовыйРегистр");

            Console.WriteLine(register.Uuid); // cd6235da-2df9-4ec4-a39b-b40f2ca21009
        }
        [TestMethod] public void WriteDBNamesToFile()
        {
            using (ConfigFileReader reader = new(DatabaseProvider.SqlServer, MS_CONNECTION_STRING, ConfigTables.Params, ConfigFiles.DbNames))
            {
                using (StreamWriter stream = new StreamWriter("C:\\temp\\DBNames.txt", false, Encoding.UTF8))
                {
                    stream.Write(reader.Stream.ReadToEnd());
                }
            }
        }
        [TestMethod] public void WriteIBParamsToFile()
        {
            using (ConfigFileReader reader = new(DatabaseProvider.SqlServer, MS_CONNECTION_STRING, ConfigTables.Params, "ibparams.inf"))
            {
                using (StreamWriter stream = new StreamWriter("C:\\temp\\IBParams.txt", false, Encoding.UTF8))
                {
                    stream.Write(reader.Stream.ReadToEnd());
                }
            }
        }
        [TestMethod] public void WriteRowDataToFile()
        {
            // Предопределённые значения "Справочник.СправочникПредопределённые"
            string fileName = "29f879f3-b889-4745-8dec-c3e18da8f84c.1c"; // dajet-metadata-ms
            string template = "4d729785-05c3-4787-ad67-00c747fbadd7.0"; // Макет-файл
            string template1 = "b3d85421-0e99-4582-b51b-f98ee692b49b.0";

            using (ConfigFileReader reader = new(DatabaseProvider.SqlServer, MS_CONNECTION_STRING, ConfigTables.Config, template1))
            {
                //using (StreamWriter stream = new StreamWriter("C:\\temp\\Справочник.СправочникПредопределённые.txt", false, Encoding.UTF8))
                //{
                //    stream.Write(reader.Stream.ReadToEnd());
                //}
                using (StreamWriter stream = new StreamWriter("C:\\temp\\РегистрСведений.ТестовыйРегистр.Макет1.Файл.txt", false, Encoding.UTF8))
                {
                    stream.Write(reader.Stream.ReadToEnd());
                }
            }
        }
        [TestMethod] public void WriteConfigObjectToFile()
        {
            //{ [{ cd6235da-2df9-4ec4-a39b-b40f2ca21009}, ТестовыйРегистр]}
            string fileName = "cd6235da-2df9-4ec4-a39b-b40f2ca21009"; // dajet-metadata-ms
            string template = "4d729785-05c3-4787-ad67-00c747fbadd7.0"; // Макет
            string template1 = "b3d85421-0e99-4582-b51b-f98ee692b49b";

            // Предопределённые значения "Справочник.СправочникПредопределённые"
            //string fileName = "29f879f3-b889-4745-8dec-c3e18da8f84c.1c"; // dajet-metadata-ms

            using (ConfigFileReader reader = new(DatabaseProvider.SqlServer, MS_CONNECTION_STRING, ConfigTables.Config, template1))
            {
                ConfigObject configObject = new ConfigFileParser().Parse(reader);

                //new ConfigFileWriter().Write(configObject, "C:\\temp\\РегистрСведений.ТестовыйРегистр.txt");
                new ConfigFileWriter().Write(configObject, "C:\\temp\\РегистрСведений.ТестовыйРегистр.Макет1.txt");
            }
        }
    }
}