using DaJet.Metadata.Core;
using System;
using System.IO;

namespace DaJet.Metadata.Extensions
{
    public static class ExtensionRootFileParser
    {
        public static bool TryParse(in ConfigFileOptions options, in ExtensionInfo extension, out string error)
        {
            error = string.Empty;

            try
            {
                ParseRootFile(in options, in extension);
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return string.IsNullOrWhiteSpace(error);
        }
        public static Guid Parse(in OneDbMetadataProvider cache, in ExtensionInfo extension)
        {
            ConfigFileOptions options = new()
            {
                FileName = extension.RootFile,
                TableName = ConfigTables.ConfigCAS,
                DatabaseProvider = cache.DatabaseProvider,
                ConnectionString = cache.ConnectionString
            };

            ParseRootFile(in options, in extension);

            return extension.Uuid;
        }
        private static void ParseRootFile(in ConfigFileOptions options, in ExtensionInfo extension)
        {
            int count = 0;
            int version = 0;
            string root = string.Empty;

            ConfigObject config;

            using (ConfigFileReader reader0 = new(options.DatabaseProvider, options.ConnectionString, options.TableName, options.FileName))
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

            if (count == 0) { return; }

            int next = 1;

            for (int i = 0; i < count; i++)
            {
                string key = config.GetString(next + i);
                string value = config.GetString(next + i + 1);
                next++;

                byte[] hex = Convert.FromBase64String(value);
                string file = Convert.ToHexString(hex).ToLower();

                _ = extension.FileMap.TryAdd(key, file);

                if (key == root)
                {
                    extension.FileName = file;
                }
            }
        }
    }
}