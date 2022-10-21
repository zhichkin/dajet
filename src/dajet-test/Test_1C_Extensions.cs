using DaJet.Data;
using DaJet.Metadata.Core;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Data;
using System.Text;

namespace DaJet.Metadata.Test
{
    [TestClass] public class Test_1C_Extensions
    {
        private const string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata-ms;Integrated Security=True;Encrypt=False;";

        [TestMethod] public void WriteExtensionInfoToFile()
        {
            byte[] buffer = Array.Empty<byte>();

            using (SqlConnection connection = new(MS_CONNECTION_STRING))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText =
                        "SELECT TOP 1 " +
                        "0 AS UTF8, " +
                        "_ExtensionZippedInfo AS ExtensionInfo, " +
                        "CAST(DATALENGTH(_ExtensionZippedInfo) AS int) AS DataSize " +
                        "FROM _ExtensionsInfo;";

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            buffer = (byte[])reader[1];
                        }
                    }
                }
            }

            //File.WriteAllBytes("C:\\temp\\РасширениеКонфигурации.zip", buffer);

            Console.WriteLine($"Is Little Endian = {BitConverter.IsLittleEndian}");

            using (MemoryStream memory = new(buffer))
            {
                using (StreamReader stream = new(memory, Encoding.UTF8))
                {
                    //using (ConfigFileReader reader = new(stream))
                    //{
                    //ConfigObject configObject = new ConfigFileParser().Parse(reader);

                    //new ConfigFileWriter().Write(configObject, "C:\\temp\\РасширениеКонфигурации.txt");

                    using (StreamWriter writer = new("C:\\temp\\РасширениеКонфигурации.txt", false, Encoding.UTF8))
                    {
                        //writer.Write(reader.Stream.ReadToEnd());

                        writer.Write(stream.ReadToEnd());
                    }
                    //}
                }
            }

            Console.WriteLine("done");
        }
        [TestMethod] public void WriteConfigCASToFile()
        {
            string fileName = "8d396154e2335dc95bfbdcbcc06dc7faed66efd6";

            using (ConfigFileReader reader = new(DatabaseProvider.SqlServer, MS_CONNECTION_STRING, ConfigTables.ConfigCAS, fileName))
            {
                ConfigObject configObject = new ConfigFileParser().Parse(reader);

                new ConfigFileWriter().Write(configObject, "C:\\temp\\РасширениеКонфигурации.txt");
            }

            Console.WriteLine("done");
        }
    }
}