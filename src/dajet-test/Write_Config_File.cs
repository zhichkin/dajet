using DaJet.Data;
using DaJet.Metadata.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

namespace DaJet.Metadata.Test
{
    [TestClass] public class Write_Config_File
    {
        private const string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata-ms;Integrated Security=True;Encrypt=False;";
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

            using (ConfigFileReader reader = new(DatabaseProvider.SqlServer, MS_CONNECTION_STRING, ConfigTables.Config, fileName))
            {
                using (StreamWriter stream = new StreamWriter("C:\\temp\\Справочник.СправочникПредопределённые.txt", false, Encoding.UTF8))
                {
                    stream.Write(reader.Stream.ReadToEnd());
                }
            }
        }
        [TestMethod] public void WriteConfigObjectToFile()
        {
            //{ [{ cd6235da-2df9-4ec4-a39b-b40f2ca21009}, ТестовыйРегистр]}
            string fileName = "cd6235da-2df9-4ec4-a39b-b40f2ca21009"; // dajet-metadata-ms

            // Предопределённые значения "Справочник.СправочникПредопределённые"
            //string fileName = "29f879f3-b889-4745-8dec-c3e18da8f84c.1c"; // dajet-metadata-ms

            using (ConfigFileReader reader = new(DatabaseProvider.SqlServer, MS_CONNECTION_STRING, ConfigTables.Config, fileName))
            {
                ConfigObject configObject = new ConfigFileParser().Parse(reader);

                new ConfigFileWriter().Write(configObject, "C:\\temp\\РегистрСведений.ТестовыйРегистр.txt");
            }
        }
    }
}