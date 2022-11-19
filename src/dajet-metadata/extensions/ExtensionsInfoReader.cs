using DaJet.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;

namespace DaJet.Metadata.Extensions
{
    public sealed class ExtensionsInfoReader
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
        public ExtensionsInfoReader(MetadataCache metadata)
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

                ExtensionInfo extension = new()
                {
                    Uuid = new Guid((byte[])reader.GetValue(0)),
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

            bool unicode = (zippedInfo[37] == 0x97);

            Encoding encoding = unicode ? Encoding.Unicode : Encoding.ASCII;
            
            int chars = zippedInfo[38];
            string config = string.Empty; //encoding.GetString(zippedInfo, 39, length + 1);

            using (MemoryStream stream = new(zippedInfo, 39, zippedInfo.Length - 39))
            {
                using (StreamReader reader = new(stream, encoding))
                {
                    for (int i = 0; i < chars; i++)
                    {
                        config += (char)reader.Read();
                    }
                }
            }

            //encoding.GetChars()

            int count = encoding.GetByteCount(config);

            byte current = zippedInfo[count + 38];

            //string config = builder.ToString(); //encoding.GetString(zippedInfo, 39, length + 1); // '\0' at the end

            //int offset = length + 2;
            //encoding = (zippedInfo[38 + offset] == 0x9A) ? Encoding.ASCII : Encoding.UTF8;
            //length = zippedInfo[38 + offset + 1];

            //extension.Version = encoding.GetString(zippedInfo, 38 + offset +  1, length);

            //extension.IsActive = (zippedInfo[38 + offset + length + 2] == 0x82);
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