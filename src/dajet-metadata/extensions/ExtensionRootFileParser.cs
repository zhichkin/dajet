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
            string uuid = string.Empty;

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
                        uuid = config.GetString(1); // uuid корневого файла описания метаданных расширения
                        extension.Uuid = new Guid(uuid);

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

                extension.FileMap.Add(new Guid(key), file);

                if (key == uuid)
                {
                    extension.FileName = file;
                }
            }

            return extension.Uuid;
        }
    }
}