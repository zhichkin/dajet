using DaJet.Data;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Metadata.Parsers;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;

namespace DaJet.Metadata.Extensions
{
    public sealed class ExtensionsManager
    {
        #region "CONSTANTS"

        private const string SELECT_EXTENSIONS =
            "SELECT _IDRRef, _ExtensionOrder, _ExtName, _UpdateTime, " +
            "_ExtensionUsePurpose, _ExtensionScope, _ExtensionZippedInfo, " +
            "_MasterNode, _UsedInDistributedInfoBase, _Version " +
            "FROM _ExtensionsInfo ORDER BY " +
            "CASE WHEN SUBSTRING(_MasterNode, CAST(1.0 AS INT), CAST(34.0 AS INT)) = N'0:00000000000000000000000000000000' " +
            "THEN 0x01 ELSE 0x00 END, _ExtensionUsePurpose, _ExtensionScope, _ExtensionOrder;";

        #endregion

        private MetadataCache _metadata;
        public ExtensionsManager(MetadataCache metadata)
        {
            _metadata = metadata;
        }
        
        public List<ExtensionInfo> GetExtensions()
        {
            List<ExtensionInfo> list = new();

            byte[] zippedInfo;

            IQueryExecutor executor = _metadata.CreateQueryExecutor();

            foreach (IDataReader reader in executor.ExecuteReader(SELECT_EXTENSIONS, 10))
            {
                zippedInfo = (byte[])reader.GetValue(6);

                Guid uuid = new(SQLHelper.Get1CUuid((byte[])reader.GetValue(0)));

                ExtensionInfo extension = new()
                {
                    Uuid = uuid, // Поле _IDRRef используется для поиска файла DbNames расширения
                    Order = (int)reader.GetDecimal(1),
                    Name = reader.GetString(2),
                    Updated = reader.GetDateTime(3).AddYears(-_metadata.InfoBase.YearOffset),
                    Purpose = (ExtensionPurpose)reader.GetDecimal(4),
                    Scope = (ExtensionScope)reader.GetDecimal(5),
                    MasterNode = reader.GetString(7),
                    IsDistributed = (((byte[])reader.GetValue(8))[0] == 1)
                };

                DecodeZippedInfo(in zippedInfo, in extension);

                list.Add(extension);
            }

            return list;
        }
        private void DecodeZippedInfo(in byte[] zippedInfo, in ExtensionInfo extension)
        {
            extension.FileName = Convert.ToHexString(zippedInfo, 4, 20).ToLower();

            Encoding encoding = (zippedInfo[37] == 0x97) ? Encoding.Unicode : Encoding.ASCII;
            
            int chars = zippedInfo[38];
            char[] buffer = new char[chars];
            
            using (MemoryStream stream = new(zippedInfo, 39, zippedInfo.Length - 39))
            {
                using (StreamReader reader = new(stream, encoding))
                {
                    for (int i = 0; i < chars; i++)
                    {
                        buffer[i] = (char)reader.Read();
                    }
                }
            }

            string config = new(buffer);
            int size = encoding.GetByteCount(config);
            byte current = zippedInfo[size + 38]; // '\0'

            using (MemoryStream memory = new(zippedInfo, 39, size))
            {
                using (StreamReader stream = new(memory, encoding))
                {
                    using (ConfigFileReader reader = new(stream))
                    {
                        ConfigObject info = new ConfigFileParser().Parse(reader);

                        extension.Alias = info[2].GetString(2);
                    }
                }
            }

            if (zippedInfo[size + 39] == 0x81) // Значение версии отсутствует
            {
                extension.IsActive = (zippedInfo[size + 40] == 0x82);
            }
            else
            {
                encoding = (zippedInfo[size + 39] == 0x97) ? Encoding.Unicode : Encoding.ASCII;
                chars = zippedInfo[size + 40];
                buffer = new char[chars];

                int offset = size + 41;

                using (MemoryStream stream = new(zippedInfo, offset, zippedInfo.Length - offset))
                {
                    using (StreamReader reader = new(stream, encoding))
                    {
                        for (int i = 0; i < chars; i++)
                        {
                            buffer[i] = (char)reader.Read();
                        }
                    }
                }

                config = new(buffer);
                size = encoding.GetByteCount(config);

                extension.Version = config;
                extension.IsActive = (zippedInfo[offset + size] == 0x82);
            }
        }

        public string GetRootFile(ExtensionInfo extension)
        {
            string fileName = extension.FileName;
            string connectionString = _metadata.ConnectionString;
            DatabaseProvider provider = _metadata.DatabaseProvider;

            using (ConfigFileReader reader = new(provider, in connectionString, ConfigTables.ConfigCAS, fileName))
            {
                return reader.Stream.ReadToEnd();
            }
        }
        public string ParseRootFile(ExtensionInfo extension, out Dictionary<string,string> files)
        {
            files = new Dictionary<string, string>();

            string fileName = extension.FileName;
            string connectionString = _metadata.ConnectionString;
            DatabaseProvider provider = _metadata.DatabaseProvider;

            int count = 0;
            int version = 0;
            string uuid = string.Empty;

            ConfigObject config;

            using (ConfigFileReader reader0 = new(provider, in connectionString, ConfigTables.ConfigCAS, fileName))
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
                return string.Empty;
            }

            int next = 1;

            for (int i = 0; i < count; i++)
            {
                string key = config.GetString(next + i);
                string value = config.GetString(next + i + 1);
                next++;

                byte[] hex = Convert.FromBase64String(value);
                string file = Convert.ToHexString(hex).ToLower();

                files.Add(key, file);

                if (key == uuid)
                {
                    extension.RootFile = file;
                }
            }

            return uuid;
        }

        public bool TryGetInfoBase(in ExtensionInfo extension, out InfoBase infoBase, out string error)
        {
            error = string.Empty;

            string fileName = extension.RootFile;
            string connectionString = _metadata.ConnectionString;
            DatabaseProvider provider = _metadata.DatabaseProvider;

            try
            {
                using (ConfigFileReader reader = new(provider, in connectionString, ConfigTables.ConfigCAS, fileName))
                {
                    new InfoBaseParser().Parse(in reader, out infoBase);
                }
            }
            catch (Exception exception)
            {
                infoBase = null;
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return (infoBase != null);
        }
        public bool TryGetMetadata(in ExtensionInfo extension, out InfoBase infoBase, out Dictionary<Guid, List<Guid>> metadata, out string error)
        {
            error = string.Empty;

            string fileName = extension.RootFile;
            string connectionString = _metadata.ConnectionString;
            DatabaseProvider provider = _metadata.DatabaseProvider;

            metadata = new Dictionary<Guid, List<Guid>>()
            {
                //{ MetadataTypes.Constant,             new List<Guid>() }, // Константы
                //{ MetadataTypes.Subsystem,            new List<Guid>() }, // Подсистемы
                { MetadataTypes.NamedDataTypeSet,     new List<Guid>() }, // Определяемые типы
                { MetadataTypes.SharedProperty,       new List<Guid>() }, // Общие реквизиты
                { MetadataTypes.Catalog,              new List<Guid>() }, // Справочники
                { MetadataTypes.Document,             new List<Guid>() }, // Документы
                { MetadataTypes.Enumeration,          new List<Guid>() }, // Перечисления
                { MetadataTypes.Publication,          new List<Guid>() }, // Планы обмена
                { MetadataTypes.Characteristic,       new List<Guid>() }, // Планы видов характеристик
                { MetadataTypes.InformationRegister,  new List<Guid>() }, // Регистры сведений
                { MetadataTypes.AccumulationRegister, new List<Guid>() }  // Регистры накопления
            };

            try
            {
                using (ConfigFileReader reader = new(provider, in connectionString, ConfigTables.ConfigCAS, fileName))
                {
                    new InfoBaseParser().Parse(in reader, out infoBase, in metadata);
                }
            }
            catch (Exception exception)
            {
                infoBase = null;
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return (infoBase != null);
        }
        public bool TryGetDbNames(in ExtensionInfo extension, out DbNameCache database, out string error)
        {
            error = string.Empty;

            string fileName = ConfigFiles.DbNames + "-Ext-" + extension.Uuid.ToString().ToLower();
            string connectionString = _metadata.ConnectionString;
            DatabaseProvider provider = _metadata.DatabaseProvider;

            database = new DbNameCache();

            try
            {
                using (ConfigFileReader reader = new(provider, in connectionString, ConfigTables.Params, fileName))
                {
                    new DbNamesParser().Parse(in reader, out database);
                }
            }
            catch (Exception exception)
            {
                database = null;
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return (database != null);
        }
        public bool TryGetMetadataObject(in string uuid, in string fileName, out MetadataObject entity, out string error)
        {
            entity = null;
            error = string.Empty;

            MetadataObjectParserFactory factory = new(_metadata);

            if (!factory.TryCreateParser(MetadataTypes.Catalog, out IMetadataObjectParser parser))
            {
                error = "Unsupported metadata type";
                return false;
            }

            string connectionString = _metadata.ConnectionString;
            DatabaseProvider provider = _metadata.DatabaseProvider;

            try
            {
                using (ConfigFileReader reader = new(provider, in connectionString, ConfigTables.ConfigCAS, fileName))
                {
                    parser.Parse(in reader, out entity);

                    if (entity.Uuid == Guid.Empty)
                    {
                        entity.Uuid = new Guid(uuid);
                    }
                }
            }
            catch (Exception exception)
            {
                entity = null;
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return (entity != null);
        }
    }
}

//Описание кодировки поля "_ExtensionZippedInfo" таблицы "_ExtensionsInfo"

//[0..1] 0x43 0xC2 - начало описания (константа)
//[2] 0x9A - тип данных char
//[3] 0x14 - количество символов = 20 байт
//[4..23] Далее идёт 20 байт - контрольная сумма расширения (SHA-1).
//Это значение является значением поля "FileName" таблицы "ConfigCAS".
//Контрольная сумма вычисляется по алгоритму SHA-1 по значению поля "BinaryData" таблицы "ConfigCAS".
//Данный файл является корневым файлом расширения (root file)
//по аналогии с корневым файлом основной конфигурации.
//Есть нюансы, но в целом всё остальное как в основной конфигурации (см. Test_1C_Extensions.cs).
//[24] 0xA2 - флаг "Защита от опасных действий" 0xA1 = false 0xA2 = true
//[25] 0x9A - тип данных char(ASCII - ?)
//[26] 0x08 - количество символов = 8 байт
//[27..34] Далее идёт 8 байт - версия изменения расширения
//Соответствует значению поля _Version в таблице _ExtensionsInfo,
//при этом почему-то на -1, то есть в СУБД значение больше на 1.
//[35] 0x82 - флаг "Безопасный режим, имя профиля безопасности" 0x81 = false 0x82 = true
//[36] 0x81 - неизвестный флаг (не используется - ?)
//[37] 0x97 - тип данных nchar(может быть 0x9A, если далее только латиница ASCII - ?)
//[38] 0x3A - количество символов(без учёта NULL в конце): описание расширения, в том числе его синоним
//[39..38+N] Формат такой же, который используется для описания объектов метаданных в файле config.
//[38+N+1] 0x00 - завершение строкового значения (NULL)
//[38+N+2] 0x9A или 0x97 - кодировка строкового значения или 0x81 = false, то есть версии нет (!)
//[38+N+3] 0x0A - длина строкового значения (без NULL в конце, как до этого)
//[38+N+4..38+N2] Далее идёт значение версии расширения, как задано в конфигураторе в поле "Версия".
//[38+N2+1] 0x81 или 0x82 - флаг "Активно" 0x81 = false 0x82 = true
//[38+N2+2] 0x81 или 0x82 - флаг "Использовать основные роли для всех пользователей" 0x81 = false 0x82 = true
//[38+N2+3] 0x20 - завершение описания (константа)

//Справочник.Расширяемый1.Реквизит.Реквизит1: Изменение типов недопустимо в режиме совместимости 8.3.17 и ниже
//При проверке метаданных обнаружены ошибки!
//Операция не может быть выполнена.