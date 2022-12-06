using DaJet.Data;
using DaJet.Metadata.Core;
using System;
using System.IO;

namespace DaJet.Metadata.Extensions
{
    public sealed class ExtensionRootFileParser
    {
        public Guid Parse(in MetadataCache cache, in ExtensionInfo extension)
        {
            string fileName = extension.RootFile;
            string connectionString = cache.ConnectionString;
            DatabaseProvider provider = cache.DatabaseProvider;

            int count = 0;
            int version = 0;
            string root = string.Empty;

            ConfigObject config;

            using (ConfigFileReader reader0 = new(provider, in connectionString, ConfigTables.ConfigCAS, in fileName))
            {
                config = new ConfigFileParser().Parse(reader0);

                version = config[1][2].GetInt32(0); // версия платформы

                StreamReader stream = reader0.Stream;

                if (!stream.EndOfStream && stream.Read() == ',')
                {
                    using (ConfigFileReader reader1 = new(stream))
                    {
                        config = new ConfigFileParser().Parse(reader1);
                        root = config.GetString(1); // uuid корневого файла описания метаданных расширения
                        extension.Uuid = new Guid(root);

                        if (!stream.EndOfStream && stream.Read() == ',')
                        {
                            using (ConfigFileReader reader2 = new(stream))
                            {
                                config = new ConfigFileParser().Parse(reader2);
                                count = config.GetInt32(0); // количество файлов описания метаданных расширения
                            }
                        }
                    }
                }
            }

            if (count == 0)
            {
                return extension.Uuid;
            }

            int next = 1;

            for (int i = 0; i < count; i++)
            {
                string key = config.GetString(next + i);
                string value = config.GetString(next + i + 1);
                next++;

                byte[] hex = Convert.FromBase64String(value);
                string file = Convert.ToHexString(hex).ToLower();

                if (Guid.TryParse(key, out Guid uuid))
                {
                    extension.FileMap.Add(uuid, file);
                }
                else
                {
                    // TODO: состав плана обмена расширения
                    // Пример uuid : 8daa2f38-04c2-4e36-83c9-8eb828e4131d.1
                    // Пример file : 52621f02c2b6a123de616603c376f3d319817441
                }

                if (key == root)
                {
                    extension.FileName = file;
                }
            }

            return extension.Uuid;
        }
    }
}