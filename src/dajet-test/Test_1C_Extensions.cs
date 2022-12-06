using DaJet.Data;
using DaJet.Metadata.Core;
using DaJet.Metadata.Extensions;
using DaJet.Metadata.Model;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Data;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace DaJet.Metadata.Test
{
    [TestClass] public class Test_1C_Extensions
    {
        private const string PG_CONNECTION_STRING = "Host=127.0.0.1;Port=5432;Database=dajet-metadata-pg;Username=postgres;Password=postgres;";
        private const string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata-ms;Integrated Security=True;Encrypt=False;";

        private readonly MetadataCache _cache;
        private readonly MetadataService _service = new();
        
        public Test_1C_Extensions()
        {
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

        #region "INVESTIGATION - OLD CODE"

        public void WriteExtensionInfoToFile()
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
                        "FROM _ExtensionsInfo WHERE _ExtensionOrder = 2;";

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            buffer = (byte[])reader[1];
                        }
                    }
                }
            }

            using (MemoryStream memory = new(buffer))
            {
                using (StreamReader stream = new(memory, Encoding.UTF8))
                {
                    using (StreamWriter writer = new("C:\\temp\\_ExtensionsInfo.txt", false, Encoding.UTF8))
                    {
                        writer.Write(stream.ReadToEnd());
                    }
                }
            }
        }
        public void WriteConfigCASToFile()
        {
            //string fileName = "69d678fcbdb163f8db132e602f018ee0d03bd9a7"; // Extension root file (see WriteRowConfigCASFile)
            string fileName = "34cc92a0d132359feddd0ca5861aff1b6dc68ed5"; // Extension config file

            //string fileName = "a0d0ee4cf0fd943378dbceaaacdc64c90cb0ea2c"; // Сотрудники
            //string fileName = "5a0b5755bb2ad8b692e973ce0dd8257ab3ad01fc"; // Номенклатура

            using (ConfigFileReader reader = new(DatabaseProvider.SqlServer, MS_CONNECTION_STRING, ConfigTables.ConfigCAS, fileName))
            {
                ConfigObject configObject = new ConfigFileParser().Parse(reader);

                new ConfigFileWriter().Write(configObject, "C:\\temp\\ExtensionConfigFile.txt");
            }
        }
        public void WriteSchemaStorageToFile()
        {
            string[] fileNames = new string[]
            {
                "CurrentSchema",
                "NewGenCreated",
                "NewGenDropped"
            };

            using (ConfigFileReader reader = new(DatabaseProvider.SqlServer, MS_CONNECTION_STRING, ConfigTables.SchemaStorage))
            {
                ConfigObject configObject = new ConfigFileParser().Parse(reader);

                new ConfigFileWriter().Write(configObject, "C:\\temp\\SchemaStorage.txt");
            }

            Console.WriteLine("done");
        }
        public void ComputeHashFromString()
        {
            string input = "_а7";
            string output = string.Empty;

            using (SHA1 sha1 = SHA1.Create())
            {
                output = Convert.ToHexString(sha1.ComputeHash(Encoding.UTF8.GetBytes(input)));
            }
            
            Console.WriteLine($"{input} = [{output}]");
        }
        public void ComputeHashFromHex()
        {
            string data = "8DD2498E9B401406E07D4B7D096F010145312D7AD1B4CDECA1186CEC1D45810DC680198C4D9493659123E50A217D82484F4F4FFA9FBED5FFE7D7EF1F1CFDFEF603F012FD7D289C00389AFBF9FEF66FBE235A566182312730802780819813192C419581F36F1AC324C3B24A7BDE3D4990E50A3B818DCBFAB37BB9A371BF42B65A8AEAB42E205642E86DD81B6641ACFBED33CD97C3526857F27E3CF9E5F8FEFEB614339E440D0ABAEB6934BD86550F71B03C04DA74588F63DD883919C2B2C2B1B2C29D401A114560254F25F1E573E49EF5997876E0CB3C5FC527893B5B7C6E7B95229AE0FB0F6792EC2C5A364560E5BD713E6D4E0DE2DC25783A5436B283B7D6A473EB4B3301CB613B58ACAB0FEBD0691C4322790B35A46F52A2DFC0E61C55CFF5C3215FA91C247727119AB8D725F8999A7CA914EA18CD0490089BE228AA8CC9A9B9573D52876317EB9D9647A71ADB4AC39750D61C3F342E626BAA96DD646128E51A71733B7D71CD4C6097FDE4A7C4DD9DDCEAD0ADC1F2720755C315B5167681CD3B2EDB5F9E685B7A695EECD4C0475F267C006D42BBDE337756361392D99C4963E8B03005C950FD5AA58E07D4A03B25E1EDF1C55D275CE8B9AAF51AD65BF77ADCCA0699F4A6AFCD5ECCF77B6B264EEDD8EEFAA9476497BEFAE701D8B89CDAEAC1FBF75D280F9A7DB7D8A1C2803D08280C62AF879E22BAD08F572B75AD05CA4CE8268AA6C0B94ACE2679AD8E6403D65A1C57E5CB7C29C165455D6A449D61A88E06FCF8EE9B402F7805F21927088C4C5481815292302A160023F0520A88222B84E71774119497FDD69AA2A4C494FA52464330A981BFC9EAF1835EFC4F69173432CCFC66C7D4039242BADAC1B446B0E38BADC729B3819324CB529C305091E7952A908933CC31429289B288793956C082BEED9176F3AE15BA9C6AF9B2ED7059E006A59E94591F3FFF02";
            string config = "BD58418E25B70DDD1BF0257EB65F804889A2B4CD450C91920E10202B6316D9659923E408B3CF2257189F2457C8ABFF7F554D8FED99762A986EA05A5D2551D423F948EA3FFFFAF7CF7CFFF1879FB565378B29300D0ED9A2042BB9851C13CF9E7D99B60F77DDA6361F42D147E8B63C108D1C5A4E39C498639349BCFC318FB64749DB336E8FF47C19EF9C97B8424051ED21B71843B31A434AC5E38C25B6993EDC6F3F7DFAA8B7E79ADB5FFE7ABBDF3EFD436F787FBB131419BDE43A570BBC080A535DA14EC6A34931A7D84B5977BEEB2A5A12E5A054B0D7901C6A4A12BA524F55D498FC9EEE6D2CEF662B94BEA9547B0F5599C25832B55A2AEA05D34866CA852D9837033638431B6D85E1B131AF9AA5E078772FF81DD3F17152C889563082DC9933CF69AB16A76D9A0D5A3447501E09BAF5146C440E5E721A4D9A980E4C2B3D679D09BBB4A1217BD3D03935288897B38C1A45A05B7CFD84DF78EC3FF7F8E1C71F5E106E66F9F0BBCF774933F7B5A63900533C66CDA12F83217D89CA76E4CAEF934435D38A29051D0D501407769638242A9347D53A68F31C287E234E80596BFBF4F1769E04277B337A1F1ADF7752452CC543C778FFEE0AEC4179FB1360D358D7809FBBF90C996B0B963485A85DBA669BB3D19D1EEEF2FF53E09DDEF0E28C5357D1D5C11370FED976C621C1A21A731D9D68B5E7E1DEE347DB99B6636101D7EDF9DA656697B45A0E5316E2AC83D22A88048C586D5422EF1351DB1E0B6F7FBEBDC0F9A3EBEBC5F57AAC8FFFD3FA7271BD5C5C9F2FAE4F17D7F3C5F5746D3D5DB41F5DB41F5DC49F2EE24757F18BD7D6B76BCBEBB5E5178D7FD1F65743F722F41723972E9A2E5DF4BCAB8E7FD175D245F4BF38FD26815152FFF3D3C75FFEF6CBDFE9A7DBAB3CA1EF5F96BCB35ADBF235F2EFDE453CF1E063948E911CA3728CF418D563D48E11C57378CAA65338E573780AA5532A9D62E994CBA75CFE4CE7532E9F5AF3B9059F27E073373E7743DD42FBB01DC374EE96CEDDD273B797CBEC6F5164BD4AD1A763D089E78EE6E36F3E31DD11DDF1DCD1DCB13C903C703C507C0ED267603E07BB54DAC5D22E9776C1B44BE6CF913C703C503C30DC11A403BF03BD038FE7E0B1EA0F753C8899FC98DE341543ABD6BC6F8D0F9A33CB88BD9166132DDD5ADE2CF4AE6AF421CF6B736D1CA878DA0A5C74DFAD18F696687DC6DE64EC9AAE996B43B7CCC2639B5A424FAEE754D5724C5D0B8DA987BA32B4546C6F99F0EF1097CEE8F4F5301BD9E8AB4A18D2075AD146C18CD1CEF538E7A4D4A21C66146D68C0527047878B0342819A3C4C07FB58EB82EEFB3087E7A4B9066FA9862C23626FF4E29132E7AE6C44F3F0DD21928705C9847664154627AF3174F2321C4D269BEF5375F1EAAD07632DC7FD83BCBD7F784D6D36C642E31F1D1A6641E7DED0F0A3936EBE209A17D93E754A8EB9AF50E682AE8E56A8C33CA101191EAB7BF67A4C555BEE708DA2DF30412E967DA0635F83E00113FD56B70E0F805E434B8336BB54B4CF8C5EA684B8720ED9C4821136E96CE8A7B1E5F01D01A9151D77292195D9438E80A156B82A5AB669B9498B6347A04CC0179583E9E6A93415DE506B60381F8B95D1B9EF213D7C6C170FA17B97901B976092D16FD5CCB374E8E76B8F7A51CF1D4E828C035D814EA805DEA0D08ACB22649FFD58B5886E77484135A3819B340096F520639459FB32ADBBD4D7C5D2FCEAC512BDEBD6E121AF2FC9F0557CE10293A6BCB03500985947195C52F59D383C6689ADCD40C91A0E6425B421312CEF3172E4A687A7BAB7B1B4D5A08E949B0B61AA0E0E8A58313517927D2AF688AD2CC337D9A26A01DB85C8C1FE31AB12BCD8CE74BC21B01C56E9C0A9321F8D6EFE55A35B9E94FD2C0136AA4F9EA863BB502077930FDB65B048E1369778D769F65E8A032B6FE44180BC46500602FEEB51160B81CC183685FD81489CA1095C620E85C727605E775E8E3AF10FF4ACBA68273A30D46F101D254A99238555A23DA7FE5E94918BCE6EF0B1B45D59664BD07ACC306DE1755103203B252DEA4250131E339FF79CE0EA16866F0E99B7CBA523CC4BAFDB755F681D610B6A68C1868D5093CDE4A4DA548F3027B6A112946CABC004F1E02E812D5A91D5568FBB022561CA66625886BF4E1E953B6139B6658E1B29C3D141688132B602D54A3F74B582CF1DAE4E257F432A3C14E0436092D1BE3175BBD124AA08203518B68246C44BE02A8391198CDB3ED5578601C05E49EDF0DCB8DE7AEE732AF8B84685A1906A00568475AB500BA96D46588B25F31115AFC098A95424120AB13F5C8C1D990104D856CAD2F26A66FD8889B89788A517CDB43105B21298C251180F28C5B595BC7830257955C28F2AA24E9DE0C6844C8608C4392AA82A5490AA4D378EF3F0A231E7685637D00AE86281B332328673ECC8E0C0E691AD3E0BEC3129B708EE7146FC778C400C488722C847564C927D19D3B45C1E57CC6A484BB91332DEE8C069C1A723481D1F4FED53348A718B7B6E98BDB17D9F30AB5BF55E3B98B0ECDE8A93CF0A9604594C10BD4C4392413161A9CFD1111271EA1BED85B044460DB367F0582CC0B1A25AA9DC8454624C60C75FA19F507AAC9110EB5140BF15DE66A54241C4D304D83CD3F84CFF595212040E25C5EC2688E1D910F8639A808F6181BD5A044926E46A90B56C6056648C66C894D38781374B05566FF49FA512B643107382F7A49A80BEAED0A2D34C49612DFD12FD526D4B242B888321E1CA1561B2501E2C9E921C2FE459543F16092B021059AB79DAB842096956E015112A75E334A6BC51691957DEBC7E50DCEEFE078EEBC430172B4232D158FEA54AAE31BA136AA488DA308F891364A72DF390A126CC36C7A992AD8D3E91175701736502E55885726BB68158DD8869BC51E961BBED86FDF67AF95F";
            byte[] input = Convert.FromHexString(data);
            string output = string.Empty;

            using (SHA1 sha1 = SHA1.Create())
            {
                output = Convert.ToHexString(sha1.ComputeHash(input));
            }

            Console.WriteLine($"{input} = [{output}]");
        }
        public void WriteExtensionZippedInfoToFile()
        {
            string data = "7B002200230022002C00380037003000320034003700330038002D0066006300320061002D0034003400330036002D0061006400610031002D006400660037003900640033003900350063003400320034002C000A007B0031002C0022007200750022002C00220020043004410448043804400435043D0438043504310022007D000A007D00";
            byte[] input = Convert.FromHexString(data);
            string output = Encoding.Unicode.GetString(input);
            Console.WriteLine(output);

            using (MemoryStream memory = new(input))
            {
                using (StreamReader stream = new(memory, Encoding.Unicode))
                {
                    using (ConfigFileReader reader = new(stream))
                    {
                        ConfigObject configObject = new ConfigFileParser().Parse(reader);

                        new ConfigFileWriter().Write(configObject, "C:\\temp\\_ExtensionZippedInfo.txt");
                    }
                }
            }
        }
        public void DumpParamsFile()
        {
            string fileName = "a07b62f0-1f01-484a-93d9-d42764cedac0.si"; // список основных таблиц объектов метаданных и их таблиц регистрации изменений

            using (ConfigFileReader reader = new(DatabaseProvider.SqlServer, MS_CONNECTION_STRING, ConfigTables.Params, fileName))
            {
                ConfigObject configObject = new ConfigFileParser().Parse(reader);

                new ConfigFileWriter().Write(configObject, "C:\\temp\\ParamsFile.txt");
            }
        }
        public void DumpConfigFile()
        {
            string fileName = "958efeec-b35c-4acc-9b42-881a0287caa4";

            using (ConfigFileReader reader = new(DatabaseProvider.SqlServer, MS_CONNECTION_STRING, ConfigTables.Config, fileName))
            {
                ConfigObject configObject = new ConfigFileParser().Parse(reader);

                new ConfigFileWriter().Write(configObject, "C:\\temp\\ConfigFile.txt");
            }
        }

        public void Dump_MetadataObject_File()
        {
            string metadataName = "Справочник.Расширяемый1";
            string outputFile = $"C:\\temp\\1c-dump\\{metadataName}.txt";

            string connectionString = _cache.ConnectionString;
            DatabaseProvider provider = _cache.DatabaseProvider;

            Catalog entity = _cache.GetMetadataObject<Catalog>(metadataName);

            using (ConfigFileReader reader = new(provider, connectionString, ConfigTables.Config, entity.Uuid))
            {
                ConfigObject configObject = new ConfigFileParser().Parse(reader);

                new ConfigFileWriter().Write(configObject, outputFile);
            }

            Console.WriteLine($"{_cache.InfoBase.Name} [{_cache.InfoBase.AppConfigVersion}]");
            Console.WriteLine();
            Console.WriteLine($"{entity.Name} [{entity.Uuid}]");
            foreach (MetadataProperty property in entity.Properties)
            {
                Console.WriteLine($" - {property.Name} [{property.Uuid}]");
            }
            foreach (TablePart table in entity.TableParts)
            {
                Console.WriteLine($"* {table.Name} [{table.Uuid}]");
                foreach (MetadataProperty property in table.Properties)
                {
                    Console.WriteLine($" - {property.Name} [{property.Uuid}]");
                }
            }
        }
        public void Dump_Extension_MetadataObject_File()
        {
            string fileName = "5497e51abcb1a0db8a048debb40d903ef3b6a7c2";
            string outputFile = "C:\\temp\\1c-dump\\Справочник.Расширяемый1.ext.txt";

            string connectionString = _cache.ConnectionString;
            DatabaseProvider provider = _cache.DatabaseProvider;

            using (ConfigFileReader reader = new(provider, connectionString, ConfigTables.ConfigCAS, fileName))
            {
                ConfigObject configObject = new ConfigFileParser().Parse(reader);

                new ConfigFileWriter().Write(configObject, outputFile);
            }

            Console.WriteLine(outputFile);
        }

        public void DeflateHex()
        {
            string hex = "7BBF7B7F352F57B5A181998E412D2F572D00";
            byte[] bin = Convert.FromHexString(hex);

            using (MemoryStream memory = new(bin))
            {
                using (DeflateStream stream = new(memory, CompressionMode.Decompress))
                {
                    using(StreamReader reader = new(stream, Encoding.UTF8))
                    {
                        Console.WriteLine(reader.ReadToEnd());
                    }
                }
            }
        }

        public void Extension_DBNames()
        {
            // Extension DBNames file: compound with _IDRRef field of _ExtensionsInfo table
            string fileName = "DBNames-Ext-5246c6ec-5177-11ed-9cda-408d5c93cc8e";

            using (ConfigFileReader reader = new(DatabaseProvider.SqlServer, MS_CONNECTION_STRING, ConfigTables.Params, fileName))
            {
                ConfigObject configObject = new ConfigFileParser().Parse(reader);

                new ConfigFileWriter().Write(configObject, "C:\\temp\\extensions\\ExtensionDBNames.txt");
            }
        }
        public void Extension_Root()
        {
            // Extension root file: SHA-1 hash value encoded in _ExtensionZippedInfo field of _ExtensionsInfo table
            // Contains three files in one: platform version, root file itself, list of metadata files (including config file)
            // Is changed each time extension metadata is changed !!!

            string fileName = "21ca0fd59bb871a891a7be309084ef23e238ab47";

            using (ConfigFileReader reader = new(DatabaseProvider.SqlServer, MS_CONNECTION_STRING, ConfigTables.ConfigCAS, fileName))
            {
                using (StreamWriter stream = new StreamWriter("C:\\temp\\extensions\\ExtensionRootFile.txt", false, Encoding.UTF8))
                {
                    stream.Write(reader.Stream.ReadToEnd());
                }
            }

            using (ConfigFileReader reader = new(DatabaseProvider.SqlServer, MS_CONNECTION_STRING, ConfigTables.Config, ConfigFiles.Root))
            {
                using (StreamWriter stream = new StreamWriter("C:\\temp\\extensions\\MainRootFile.txt", false, Encoding.UTF8))
                {
                    stream.Write(reader.Stream.ReadToEnd());
                }
            }
        }
        public void Extension_Config()
        {
            // Extension config file (obtained from extension root file): the same format as main config file have
            string fileName = "189292c3d23fdd2b1b6ae7eeaedd8c39145ba386";

            using (ConfigFileReader reader = new(DatabaseProvider.SqlServer, MS_CONNECTION_STRING, ConfigTables.ConfigCAS, fileName))
            {
                ConfigObject configObject = new ConfigFileParser().Parse(reader);

                new ConfigFileWriter().Write(configObject, "C:\\temp\\extensions\\ExtensionConfigFile.txt");
            }

            fileName = "684c8f2b-d93f-49cc-b766-b3cc3896b047";

            using (ConfigFileReader reader = new(DatabaseProvider.SqlServer, MS_CONNECTION_STRING, ConfigTables.Config, fileName))
            {
                ConfigObject configObject = new ConfigFileParser().Parse(reader);

                new ConfigFileWriter().Write(configObject, "C:\\temp\\extensions\\MainConfigFile.txt");
            }
        }
        public void Extension_Metadata()
        {
            // Extension metadata object files (obtained from extension config file)
            string[] fileNames = new string[]
            {
                //"22b81a83c912bc42ab4daeae0c5a9eac308d7025", // 060ce774-3c4f-4be3-98d8-8feb61e22662 - [Общие/Роли] Расш2_ОсновнаяРоль
                //"a0d0ee4cf0fd943378dbceaaacdc64c90cb0ea2c", // 1c66f342-27bf-4982-9fd5-a11778663e69 - заимствованный справочник "Сотрудники"
                //"5a0b5755bb2ad8b692e973ce0dd8257ab3ad01fc", // aebaa85b-3b56-4e48-8332-0351b17ab64e - заимствованный справочник "Номенклатура"
                //"db555e00687401b06a8debd281081208bc53fe0c", // f9d8d256-fe2f-483f-8663-c7f76b2197dc - [Общие/Языки] Русский
                //"529a0e42c5c063c1613fec6ddb07999b744cf5fd", // 2a96800d-54b5-44cd-ba10-725588248b0e - собственный справочник "Расш2_Справочник1"
                "faf0bedeccf874393b10db7195d04edb123840e4"
            };

            foreach (string fileName in fileNames)
            {
                using (ConfigFileReader reader = new(DatabaseProvider.SqlServer, MS_CONNECTION_STRING, ConfigTables.ConfigCAS, fileName))
                {
                    ConfigObject configObject = new ConfigFileParser().Parse(reader);

                    new ConfigFileWriter().Write(configObject, $"C:\\temp\\extensions\\{fileName}.txt");
                }
            }

            Catalog catalog = _cache.GetMetadataObject<Catalog>("Справочник.Расширяемый1");
            Console.WriteLine($"{catalog.Name} [{catalog.Uuid}]");

            foreach (MetadataProperty property in catalog.Properties)
            {
                Console.WriteLine($" - {property.Name} [{property.Uuid}]");
            }
        }

        #endregion

        [TestMethod] public void GetExtensionsMetadata()
        {
            Console.WriteLine();
            Console.WriteLine($"{_cache.InfoBase.Name} [{_cache.InfoBase.Uuid}]");

            List<ExtensionInfo> extensions = _cache.GetExtensions();

            foreach (ExtensionInfo extension in extensions)
            {
                Console.WriteLine();
                Console.WriteLine($"Root: {extension.Name} [{extension.Identity}]");

                if (!_cache.TryGetMetadata(in extension, out MetadataCache metadata, out string error))
                {
                    Console.WriteLine(error);
                }
                else
                {
                    Console.WriteLine($"Info: {metadata.InfoBase.Name} [{metadata.InfoBase.Uuid}]");
                    
                    ShowMetadataObjects(in metadata);
                }
            }
        }
        private void DumpMetadataObject(in MetadataCache metadata, in MetadataObject @object)
        {
            string fileName = metadata.Extension.FileMap[@object.Uuid];
            string outputFile = $"C:\\temp\\1c-dump\\{@object}.ext.txt";

            string connectionString = metadata.ConnectionString;
            DatabaseProvider provider = metadata.DatabaseProvider;

            using (ConfigFileReader reader = new(provider, connectionString, ConfigTables.ConfigCAS, fileName))
            {
                ConfigObject configObject = new ConfigFileParser().Parse(reader);

                new ConfigFileWriter().Write(configObject, outputFile);
            }

            Console.WriteLine(outputFile);
        }
        private void ShowMetadataObjects(in MetadataCache metadata)
        {
            foreach (MetadataItem item in metadata.GetMetadataItems(MetadataTypes.Characteristic))
            {
                Characteristic entity = metadata.GetMetadataObject<Characteristic>(item);

                string fileName = metadata.Extension.FileMap[entity.Uuid];

                Console.WriteLine($"* {entity.Name} {entity.Parent} >> {{{entity.Uuid}}} [{fileName}]");
                Console.WriteLine($"> {entity.DataTypeSet.GetDescription()}");
                Console.WriteLine($">> {entity.ExtensionDataTypeSet?.GetDescription()}");

                MetadataObject @object = _cache.GetMetadataObject(entity.ToString());

                if (@object is Characteristic parent)
                {
                    Console.WriteLine($"- {parent.Name} {parent.Uuid}");
                    Console.WriteLine($"> {parent.DataTypeSet.GetDescription()}");
                    Console.WriteLine($">> {parent.ExtensionDataTypeSet?.GetDescription()}");
                }

                DumpMetadataObject(in metadata, entity);

                //foreach (MetadataProperty property in catalog.Properties)
                //{
                //    Console.WriteLine($"  - {property.Name} [{property.DbName}]");
                //}
            }
        }

        [TestMethod] public void GetExtensionTablePart()
        {
            Console.WriteLine();
            Console.WriteLine($"{_cache.InfoBase.Name} {{{_cache.InfoBase.Uuid}}}");

            List<ExtensionInfo> extensions = _cache.GetExtensions();

            foreach (ExtensionInfo extension in extensions)
            {
                Console.WriteLine();
                Console.WriteLine($"Root: {extension.Name} {{{extension.Identity}}} [{extension.RootFile}]");

                if (!_cache.TryGetMetadata(in extension, out MetadataCache metadata, out string error))
                {
                    Console.WriteLine(error);
                }
                else
                {
                    Console.WriteLine($"Info: {metadata.InfoBase.Name} {{{metadata.InfoBase.Uuid}}} [{extension.FileName}]");
                    Console.WriteLine();

                    ShowTablePartObjects(in metadata);
                }
            }
        }
        private void ShowTablePartObjects(in MetadataCache metadata)
        {
            foreach (MetadataItem item in metadata.GetMetadataItems(MetadataTypes.Document))
            {
                MetadataObject entity = metadata.GetMetadataObject(item);

                if (entity is ITablePartOwner owner)
                {
                    if (owner.TableParts.Count > 0)
                    {
                        string fileName = metadata.Extension.FileMap[entity.Uuid];
                        Console.WriteLine($"+ {entity.Name} {entity.Parent} >> {{{entity.Uuid}}} [{fileName}]");

                        MetadataObject parent = _cache.GetMetadataObject(entity.ToString());
                        Console.WriteLine($"> {parent?.Name} {parent?.Uuid}");

                        ShowObjectProperties(in metadata, in entity);

                        ShowTablePartObjects(in metadata, in entity);
                    }
                }
            }
        }
        private void ShowTablePartObjects(in MetadataCache metadata, in MetadataObject @object)
        {
            if (@object is not Document entity)
            {
                return;
            }

            foreach (TablePart table in entity.TableParts)
            {
                Console.WriteLine();
                Console.WriteLine($"+ {table.Name} {table.Parent} >> {{{table.Uuid}}}");

                string tableName = $"Документ.{entity.Name}.{table.Name}";
                TablePart parentTable = _cache.GetMetadataObject<TablePart>(tableName);
                if (parentTable == null)
                {
                    Console.WriteLine($"> Parent table part is absent");
                }
                else
                {
                    Console.WriteLine($"> {parentTable.Name} {parentTable.Uuid}");
                }

                ShowObjectProperties(in metadata, table);
            }
        }
        private void ShowObjectProperties(in MetadataCache metadata, in MetadataObject @object)
        {
            if (@object is not ApplicationObject entity)
            {
                return;
            }

            foreach (MetadataProperty property in entity.Properties)
            {
                Console.WriteLine($"@ {property.Name} {{{property.Uuid}}} >> {{{property.Parent}}}");
                Console.WriteLine($"= {property.PropertyType.GetDescription()}");
                Console.WriteLine($"> {property.ExtensionPropertyType?.GetDescription()}");
            }
        }

        [TestMethod] public void GetExtensionSharedProperty()
        {
            Console.WriteLine();
            Console.WriteLine($"{_cache.InfoBase.Name} {{{_cache.InfoBase.Uuid}}}");

            List<ExtensionInfo> extensions = _cache.GetExtensions();

            foreach (ExtensionInfo extension in extensions)
            {
                Console.WriteLine();
                Console.WriteLine($"Root: {extension.Name} {{{extension.Identity}}} [{extension.RootFile}]");

                if (!_cache.TryGetMetadata(in extension, out MetadataCache metadata, out string error))
                {
                    Console.WriteLine(error);
                }
                else
                {
                    Console.WriteLine($"Info: {metadata.InfoBase.Name} {{{metadata.InfoBase.Uuid}}} [{extension.FileName}]");
                    Console.WriteLine();

                    ShowSharedProperty(in metadata);
                }
            }
        }
        private void ShowSharedProperty(in MetadataCache metadata)
        {
            foreach (MetadataItem item in metadata.GetMetadataItems(MetadataTypes.SharedProperty))
            {
                MetadataObject entity = metadata.GetMetadataObject(item);

                string fileName = metadata.Extension.FileMap[entity.Uuid];
                Console.WriteLine($"+ {entity.Name} {entity.Parent} >> {{{entity.Uuid}}} [{fileName}]");

                MetadataObject parent = _cache.GetMetadataObject("SharedProperty." + entity.ToString());
                Console.WriteLine($"> {parent?.Name} {parent?.Uuid}");
            }
        }

        [TestMethod] public void GetExtensionPublication()
        {
            Console.WriteLine();
            Console.WriteLine($"{_cache.InfoBase.Name} {{{_cache.InfoBase.Uuid}}}");

            List<ExtensionInfo> extensions = _cache.GetExtensions();

            foreach (ExtensionInfo extension in extensions)
            {
                Console.WriteLine();
                Console.WriteLine($"Root: {extension.Name} {{{extension.Identity}}} [{extension.RootFile}]");

                if (!_cache.TryGetMetadata(in extension, out MetadataCache metadata, out string error))
                {
                    Console.WriteLine(error);
                }
                else
                {
                    Console.WriteLine($"Info: {metadata.InfoBase.Name} {{{metadata.InfoBase.Uuid}}} [{extension.FileName}]");
                    Console.WriteLine();

                    ShowPublication(in metadata);
                }
            }
        }
        private void ShowPublication(in MetadataCache metadata)
        {
            foreach (MetadataItem item in metadata.GetMetadataItems(MetadataTypes.Publication))
            {
                MetadataObject entity = metadata.GetMetadataObject(item);

                string fileName = metadata.Extension.FileMap[entity.Uuid];
                Console.WriteLine($"+ {entity.Name} {entity.Parent} >> {{{entity.Uuid}}} [{fileName}]");

                MetadataObject parent = _cache.GetMetadataObject(entity.ToString());
                Console.WriteLine($"> {parent?.Name} {parent?.Uuid}");
            }
        }

        [TestMethod] public void GetExtensionDocument()
        {
            Console.WriteLine();
            Console.WriteLine($"{_cache.InfoBase.Name} {{{_cache.InfoBase.Uuid}}}");

            ExtensionInfo extension = _cache.GetExtension("Расширение1");
            
            Console.WriteLine();
            Console.WriteLine($"Root: {extension.Name} {{{extension.Identity}}} [{extension.RootFile}]");

            if (!_cache.TryGetMetadata(in extension, out MetadataCache metadata, out string error))
            {
                Console.WriteLine(error);
            }
            else
            {
                Console.WriteLine($"Info: {metadata.InfoBase.Name} {{{metadata.InfoBase.Uuid}}} [{extension.FileName}]");

                ShowDocuments(in metadata);
            }
        }
        private void ShowDocuments(in MetadataCache metadata)
        {
            foreach (MetadataItem item in metadata.GetMetadataItems(MetadataTypes.Document))
            {
                Console.WriteLine();

                Document entity = metadata.GetMetadataObject<Document>(item);

                string fileName = metadata.Extension.FileMap[entity.Uuid];
                Console.WriteLine($"+ {entity.Name} [{entity.TableName}] {entity.Parent} >> {{{entity.Uuid}}} [{fileName}]");

                Document parent = _cache.GetMetadataObject<Document>(entity.ToString());
                Console.WriteLine($"> {parent?.Name} [{parent?.TableName}] {parent?.Uuid}");

                foreach (MetadataProperty property in entity.Properties)
                {
                    Console.WriteLine($"  - {property.Name} [{property.Columns.FirstOrDefault()}]");
                }

                foreach (TablePart table in entity.TableParts)
                {
                    Console.WriteLine($"  * {table.Name} [{table.TableName}]");

                    foreach (MetadataProperty property in table.Properties)
                    {
                        Console.WriteLine($"    - {property.Name} [{property.Columns.FirstOrDefault()}]");
                    }
                }
            }
        }
    }
}