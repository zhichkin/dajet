using DaJet.Metadata.Core;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace DaJet_Console
{
    public static class DataHistory
    {
        private const string PG_CONNECTION_STRING = "Host=127.0.0.1;Port=5432;Database=dajet-exchange;Username=postgres;Password=postgres;";
        private const string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=dajet-exchange;Integrated Security=True;Encrypt=False;";
        private const string SELECT_METADATA_SCRIPT =
            "SELECT " +
            "CASE WHEN SUBSTRING(_Content, 1, 3) = 0xEFBBBF THEN 1 ELSE 0 END AS UTF8, DATALENGTH(_Content), _Content " +
            "FROM _DataHistoryMetadata " +
            "WHERE _MetadataId = @metadata AND _MetadataVersionNumber = @version;";
        private const string SELECT_CONTENT_SCRIPT =
            "SELECT TOP 10 " +
            "CASE WHEN SUBSTRING(_Content, 1, 3) = 0xEFBBBF THEN 1 ELSE 0 END AS UTF8, DATALENGTH(_Content), _Content " +
            "FROM _DataHistoryQueue0 " +
            "ORDER BY _MetadataId ASC, _DataId ASC, _Position ASC;";

        public static StreamReader CreateReader(in byte[] fileData, bool utf8)
        {
            MemoryStream memory = new(fileData);

            if (utf8)
            {
                return CreateStreamReader(in memory);
            }
            return CreateDeflateReader(in memory);
        }
        private static StreamReader CreateStreamReader(in MemoryStream memory)
        {
            return new StreamReader(memory, Encoding.UTF8);
        }
        private static StreamReader CreateDeflateReader(in MemoryStream memory)
        {
            DeflateStream stream = new(memory, CompressionMode.Decompress);
            return new StreamReader(stream, Encoding.UTF8);
        }
        public static void ComputeHashFromHex()
        {
            string data = "8DD2498E9B401406E07D4B7D096F010145312D7AD1B4CDECA1186CEC1D45810DC680198C4D9493659123E50A217D82484F4F4FFA9FBED5FFE7D7EF1F1CFDFEF603F012FD7D289C00389AFBF9FEF66FBE235A566182312730802780819813192C419581F36F1AC324C3B24A7BDE3D4990E50A3B818DCBFAB37BB9A371BF42B65A8AEAB42E205642E86DD81B6641ACFBED33CD97C3526857F27E3CF9E5F8FEFEB614339E440D0ABAEB6934BD86550F71B03C04DA74588F63DD883919C2B2C2B1B2C29D401A114560254F25F1E573E49EF5997876E0CB3C5FC527893B5B7C6E7B95229AE0FB0F6792EC2C5A364560E5BD713E6D4E0DE2DC25783A5436B283B7D6A473EB4B3301CB613B58ACAB0FEBD0691C4322790B35A46F52A2DFC0E61C55CFF5C3215FA91C247727119AB8D725F8999A7CA914EA18CD0490089BE228AA8CC9A9B9573D52876317EB9D9647A71ADB4AC39750D61C3F342E626BAA96DD646128E51A71733B7D71CD4C6097FDE4A7C4DD9DDCEAD0ADC1F2720755C315B5167681CD3B2EDB5F9E685B7A695EECD4C0475F267C006D42BBDE337756361392D99C4963E8B03005C950FD5AA58E07D4A03B25E1EDF1C55D275CE8B9AAF51AD65BF77ADCCA0699F4A6AFCD5ECCF77B6B264EEDD8EEFAA9476497BEFAE701D8B89CDAEAC1FBF75D280F9A7DB7D8A1C2803D08280C62AF879E22BAD08F572B75AD05CA4CE8268AA6C0B94ACE2679AD8E6403D65A1C57E5CB7C29C165455D6A449D61A88E06FCF8EE9B402F7805F21927088C4C5481815292302A160023F0520A88222B84E71774119497FDD69AA2A4C494FA52464330A981BFC9EAF1835EFC4F69173432CCFC66C7D4039242BADAC1B446B0E38BADC729B3819324CB529C305091E7952A908933CC31429289B288793956C082BEED9176F3AE15BA9C6AF9B2ED7059E006A59E94591F3FFF02";
            byte[] input = Convert.FromHexString(data);
            string output = Convert.ToHexString(SHA1.HashData(input));
            Console.WriteLine($"{input} = [{output}]");
        }
        public static string GetMetadata()
        {
            byte[] buffer = null;
            int bufferSize = 0;
            bool utf8 = false;
            long bytesRead = 0L;

            using (SqlConnection connection = new(MS_CONNECTION_STRING))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = SELECT_METADATA_SCRIPT;
                    command.Parameters.AddWithValue("metadata", new Guid("220213B0-027D-BD3E-43A7-7FEA0F2965A9").ToByteArray());
                    command.Parameters.AddWithValue("version", 2);

                    using (DbDataReader reader = command.ExecuteReader(CommandBehavior.SequentialAccess))
                    {
                        while (reader.Read())
                        {
                            utf8 = (reader.GetInt32(0) == 1);
                            bufferSize = (int)reader.GetInt64(1);
                            buffer = new byte[bufferSize];
                            bytesRead = reader.GetBytes(2, 0L, buffer, 0, bufferSize);
                        }
                    }
                }
            }

            utf8 = true;
            byte[] data = new byte[bufferSize - 70];
            Array.Copy(buffer, 70, data, 0, data.Length);

            using (StreamReader stream = CreateReader(in data, utf8))
            {
                return stream.ReadToEnd();

                //using (ConfigFileReader reader = new(stream))
                //{
                //    ConfigObject content = new ConfigFileParser().Parse(in reader);

                //    new ConfigFileWriter().Write(content, "C:\\temp\\1c-dump\\ИсторияДанных.txt");

                //    using (StreamReader file = new("C:\\temp\\1c-dump\\ИсторияДанных.txt", Encoding.UTF8))
                //    {
                //        return file.ReadToEnd();
                //    }
                //}
            }
        }
        public static string GetContent()
        {
            byte[] buffer = null;
            int bufferSize;
            bool utf8 = false;
            long bytesRead = 0L;

            using (SqlConnection connection = new(MS_CONNECTION_STRING))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = SELECT_CONTENT_SCRIPT;

                    using (DbDataReader reader = command.ExecuteReader(CommandBehavior.SequentialAccess))
                    {
                        while (reader.Read())
                        {
                            utf8 = (reader.GetInt32(0) == 1);
                            bufferSize = (int)reader.GetInt64(1);
                            buffer = new byte[bufferSize];
                            bytesRead = reader.GetBytes(2, 0L, buffer, 0, bufferSize);
                        }
                    }
                }
            }

            using (StreamReader stream = CreateReader(in buffer, true))
            {
                return stream.ReadToEnd();
            }
        }
        public static void GetDateTimeFromBinary()
        {
            string data = "904E";
            byte[] input = Convert.FromHexString(data);
            //string output = Convert.ToHexString(SHA1.HashData(input));

            long one = BitConverter.ToInt64(Convert.FromHexString("80164EF8F57F9F48"));
            long two = BitConverter.ToInt64(Convert.FromHexString("0080B5F7F57F9F48"));
            string output = Convert.ToHexString(BitConverter.GetBytes(DateTime.UnixEpoch.AddSeconds(1).ToBinary()));

            Console.WriteLine(output);

            Console.WriteLine(DateTime.FromBinary(BitConverter.ToInt64(Convert.FromHexString("90CEA180CE8A9101"))));
            Console.WriteLine(DateTime.FromFileTime(BitConverter.ToInt64(Convert.FromHexString("90CEA180CE8A9101"))));
            Console.WriteLine(DateTime.FromOADate(BitConverter.ToDouble(Convert.FromHexString("90CEA180CE8A9101"))));

            Console.WriteLine(one - two);
        }
    }
}