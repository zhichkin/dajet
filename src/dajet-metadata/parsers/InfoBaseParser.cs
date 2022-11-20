using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class InfoBaseParser
    {
        private ConfigFileParser _parser;
        private ConfigFileConverter _converter;
        private readonly ConfigFileTokenHandler _metadataHandler; // cache delegate for re-use

        private InfoBase _infoBase;
        private Dictionary<Guid, List<Guid>> _metadata;
        public InfoBaseParser()
        {
            _metadataHandler = new ConfigFileTokenHandler(MetadataCollection);
        }
        public void Parse(in ConfigFileReader reader, out InfoBase infoBase)
        {
            _infoBase = new InfoBase()
            {
                YearOffset = reader.YearOffset,
                PlatformVersion = reader.PlatformVersion
            };

            if (Guid.TryParse(reader.FileName, out Guid uuid))
            {
                _infoBase.Uuid = uuid;
            }

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            if (_infoBase.Uuid == Guid.Empty)
            {
                // Контрольная сумма SHA-1 по полю "BinaryData" таблицы "ConfigCAS"
                _converter[1][0] += FileName; // Случай для расширения конфигурации
            }

            ConfigureInfoBaseConverter();
            
            _converter[3][1][1] += Cancel; // Прервать чтение файла после прочтения свойств конфигурации

            _parser.Parse(in reader, in _converter);

            // Parsing result
            infoBase = _infoBase;

            // Dispose private variables
            _infoBase = null;
            _parser = null;
            _converter = null;
        }
        public void Parse(in ConfigFileReader reader, out InfoBase infoBase, in Dictionary<Guid, List<Guid>> metadata)
        {
            _infoBase = new InfoBase()
            {
                YearOffset = reader.YearOffset,
                PlatformVersion = reader.PlatformVersion
            };

            if (Guid.TryParse(reader.FileName, out Guid uuid))
            {
                _infoBase.Uuid = uuid;
            }

            _metadata = metadata;

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            if (_infoBase.Uuid == Guid.Empty)
            {
                // Контрольная сумма SHA-1 по полю "BinaryData" таблицы "ConfigCAS"
                _converter[1][0] += FileName; // Случай для расширения конфигурации
            }

            ConfigureInfoBaseConverter();
            ConfigureMetadataConverter();

            _parser.Parse(in reader, in _converter);

            // Parsing results
            infoBase = _infoBase;

            // Dispose private variables
            _infoBase = null;
            _metadata = null;
            _parser = null;
            _converter = null;
        }
        private void ConfigureInfoBaseConverter()
        {
            // DONE: take file name from reader
            //_converter[1][0] += FileName; // Значение поля FileName в таблице Config

            // Свойства конфигурации
            _converter[3][1][1][1][1][2] += Name; // Наименование конфигурации
            _converter[3][1][1][1][1][3][2] += Alias; // Синоним
            _converter[3][1][1][1][1][4] += Comment; // Комментарий
            _converter[3][1][1][15] += ConfigVersion; // Версия конфигурации
            _converter[3][1][1][26] += Version; // Режим совместимости
            _converter[3][1][1][41] += SyncCallsMode; // Режим использования синхронных вызовов расширений платформы и внешних компонент
            _converter[3][1][1][36] += ModalWindowMode; // Режим использования модальности
            _converter[3][1][1][17] += DataLockingMode; // Режим управления блокировкой данных в транзакции по умолчанию
            _converter[3][1][1][19] += AutoNumberingMode; // Режим автонумерации объектов
            _converter[3][1][1][38] += UICompatibilityMode; // Режим совместимости интерфейса

            // Свойства расширения конфигурации
            _converter[3][1][1][42] += NamePrefix;
            _converter[3][1][1][43] += ExtensionCompatibility;
            _converter[3][1][1][49] += MapMetadataByUuid;
        }
        private void ConfigureMetadataConverter()
        {
            // Коллекция объектов метаданных
            _converter[2] += ConfigureComponents;
        }
        private void Cancel(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (source.Token == TokenType.EndObject)
            {
                args.Cancel = true;
            }
        }

        #region "Свойства конфигурации"

        private void FileName(in ConfigFileReader source, in CancelEventArgs args)
        {
            _infoBase.Uuid = source.GetUuid();
        }
        private void Name(in ConfigFileReader source, in CancelEventArgs args)
        {
            _infoBase.Name = source.Value;
        }
        private void Alias(in ConfigFileReader source, in CancelEventArgs args)
        {
            _infoBase.Alias = source.Value;
        }
        private void Comment(in ConfigFileReader source, in CancelEventArgs args)
        {
            _infoBase.Comment = source.Value;
        }
        private void Version(in ConfigFileReader source, in CancelEventArgs args)
        {
            int version = source.GetInt32();

            if (version == 0)
            {
                _infoBase.CompatibilityVersion = 80216;
            }
            else if (version == 1)
            {
                _infoBase.CompatibilityVersion = 80100;
            }
            else if (version == 2)
            {
                _infoBase.CompatibilityVersion = 80213;
            }
            else
            {
                _infoBase.CompatibilityVersion = version;
            }
        }
        private void ConfigVersion(in ConfigFileReader source, in CancelEventArgs args)
        {
            _infoBase.AppConfigVersion = source.Value;
        }
        private void SyncCallsMode(in ConfigFileReader source, in CancelEventArgs args)
        {
            _infoBase.SyncCallsMode = (SyncCallsMode)source.GetInt32();
        }
        private void ModalWindowMode(in ConfigFileReader source, in CancelEventArgs args)
        {
            _infoBase.ModalWindowMode = (ModalWindowMode)source.GetInt32();
        }
        private void DataLockingMode(in ConfigFileReader source, in CancelEventArgs args)
        {
            _infoBase.DataLockingMode = (DataLockingMode)source.GetInt32();
        }
        private void AutoNumberingMode(in ConfigFileReader source, in CancelEventArgs args)
        {
            _infoBase.AutoNumberingMode = (AutoNumberingMode)source.GetInt32();
        }
        private void UICompatibilityMode(in ConfigFileReader source, in CancelEventArgs args)
        {
            _infoBase.UICompatibilityMode = (UICompatibilityMode)source.GetInt32();
        }
        private void NamePrefix(in ConfigFileReader source, in CancelEventArgs args)
        {
            _infoBase.NamePrefix = source.Value;
        }
        private void MapMetadataByUuid(in ConfigFileReader source, in CancelEventArgs args)
        {
            _infoBase.MapMetadataByUuid = (source.GetInt32() == 1);
        }
        private void ExtensionCompatibility(in ConfigFileReader source, in CancelEventArgs args)
        {
            _infoBase.ExtensionCompatibility = source.GetInt32();
        }

        #endregion

        #region "Коллекция объектов метаданных"

        private void ConfigureComponents(in ConfigFileReader source, in CancelEventArgs args)
        {
            int offset = 2; // текущая позиция [2] - последующие позиции +1
            int count = source.GetInt32(); // количество компонентов платформы

            while (count > 0)
            {
                _converter[offset + count][0] += ConfigureComponent;
                count--;
            }
        }
        private void ConfigureComponent(in ConfigFileReader source, in CancelEventArgs args)
        {
            Guid uuid = source.GetUuid(); // Идентификатор компоненты платформы

            // Родительский узел компоненты платформы
            // NOTE: Последовательность узлов компонентов платформы может быть не гарантирована.
            int node = source.Path[0];

            if (uuid == Components.General) // 3.0 - Компонента платформы "Общие объекты"
            {
                _converter[node][1][2] += ConfigureMetadata; // начало коллекции объектов метаданных компоненты
            }
            else if (uuid == Components.Operations) // 4.0 - Компонента платформы "Оперативный учёт"
            {
                _converter[node][1][1][2] += ConfigureMetadata; // начало коллекции объектов метаданных компоненты
            }   
        }
        private void ConfigureMetadata(in ConfigFileReader source, in CancelEventArgs args)
        {
            int count = source.GetInt32(); // количество объектов метаданных компоненты
            int offset = source.Path[source.Level]; // 3.1.2 текущая позиция - последующие позиции +1
            
            ConfigFileConverter node = _converter.Path(source.Level - 1, source.Path); // родительский узел компоненты
            
            while (count > 0)
            {
                node[offset + count] += _metadataHandler;
                count--;
            }
        }
        private void MetadataCollection(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (source.Token == TokenType.EndObject)
            {
                return;
            }

            int count = 0;
            Guid type = Guid.Empty;

            if (source.Read()) // 0 - Идентификатор общего типа объекта метаданных
            {
                type = source.GetUuid();
            }

            if (!_metadata.TryGetValue(type, out List<Guid> collection))
            {
                return; // Неподдерживаемый или неизвестный тип объекта метаданных
            }

            if (source.Read()) // 1 - Количество объектов метаданных в коллекции
            {
                count = source.GetInt32();
            }

            while (count > 0)
            {
                if (!source.Read())
                {
                    break;
                }

                Guid uuid = source.GetUuid(); // Идентификатор объекта метаданных

                if (uuid != Guid.Empty)
                {
                    collection.Add(uuid);
                }

                count--;
            }
        }

        #endregion
    }
}