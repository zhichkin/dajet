using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System.Collections.Generic;
using System.ComponentModel;
using System;

namespace DaJet.Metadata.Parsers
{
    public sealed class AccountParser : IMetadataObjectParser
    {
        private readonly OneDbMetadataProvider _cache;
        private ConfigFileParser _parser;
        private TablePartCollectionParser _tableParser;
        private MetadataPropertyCollectionParser _propertyParser;

        private Account _target;
        private MetadataInfo _entry;
        private ConfigFileConverter _converter;
        public AccountParser() { }
        public AccountParser(OneDbMetadataProvider cache) { _cache = cache; }
        public void Parse(in ConfigFileOptions options, out MetadataInfo info)
        {
            _entry = new MetadataInfo()
            {
                MetadataUuid = options.MetadataUuid,
                MetadataType = MetadataTypes.Account
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.15.1.1.2 - uuid объекта метаданных (FileName)

            _converter[1][3] += Reference;  // Идентификатор ссылочного типа данных, например, "ПланСчетовСсылка.Управленческий"
            _converter[1][15][1][2] += Name; // Имя объекта метаданных конфигурации

            using (ConfigFileReader reader = new(options.DatabaseProvider, options.ConnectionString, options.TableName, options.FileName))
            {
                _parser.Parse(in reader, in _converter);
            }

            info = _entry;

            _entry = null;
            _parser = null;
            _converter = null;
        }
        public void Parse(in ConfigFileReader source, Guid uuid, out MetadataInfo target)
        {
            _entry = new MetadataInfo()
            {
                MetadataUuid = uuid,
                MetadataType = MetadataTypes.Account
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.15.1.1.2 - uuid объекта метаданных (FileName)

            _converter[1][3] += Reference;  // Идентификатор ссылочного типа данных, например, "ПланСчетовСсылка.Управленческий"
            _converter[1][15][1][2] += Name; // Имя объекта метаданных конфигурации

            _parser.Parse(in source, in _converter);

            target = _entry;

            _entry = null;
            _parser = null;
            _converter = null;
        }
        public void Parse(in ConfigFileReader reader, Guid uuid, out MetadataObject target)
        {
            _target = new Account() { Uuid = uuid };

            ConfigureConverter();

            _parser = new ConfigFileParser();
            _tableParser = new TablePartCollectionParser(_cache);
            _propertyParser = new MetadataPropertyCollectionParser(_cache, _target);

            _parser.Parse(in reader, in _converter);

            // result
            target = _target;

            // dispose private variables
            _target = null;
            _parser = null;
            _converter = null;
            _tableParser = null;
            _propertyParser = null;
        }
        private void ConfigureConverter()
        {
            _converter = new ConfigFileConverter();

            _converter[1][15][1][2] += Name; // Имя объекта метаданных
            _converter[1][15][1][3][2] += Alias; // Синоним (представление)
            _converter[1][19] += DimensionTypes; // Виды субконто (план видов характеристик)
            _converter[1][20] += MaxDimensionCount; // Максимальное количество субконто
            _converter[1][21] += CodeMask; // Маска кода (строка)
            _converter[1][22] += CodeLength; // Длина кода (всегда строка)
            _converter[1][23] += DescriptionLength; // Длина наименования
            _converter[1][24] += UseAutoOrder; // Автопорядок по коду
            _converter[1][25] += AutoOrderLength; // Длина порядка

            _converter[5] += TablePartCollection; // 4c7fec95-d1bd-4508-8a01-f1db090d9af8 - идентификатор коллекции табличных частей
            _converter[7] += PropertyCollection;  // 6e65cbf5-daa8-4d8d-bef8-59723f4e5777 - идентификатор коллекции реквизитов
            _converter[8] += PropertyCollection;  // 78bd1243-c4df-46c3-8138-e147465cb9a4 - идентификатор коллекции признаков учёта
            _converter[9] += PropertyCollection;  // c70ca527-5042-4cad-a315-dcb4007e32a3 - идентификатор коллекции признаков учёта субконто
        }
        private void Reference(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (_entry is not null)
            {
                _entry.ReferenceUuid = source.GetUuid();
            }
        }
        private void Name(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (_entry is not null)
            {
                _entry.Name = source.Value;
            }
            else if (_target is not null)
            {
                _target.Name = source.Value;
            }
        }
        private void Alias(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.Alias = source.Value;
        }
        private void CodeMask(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.CodeMask = source.Value;
        }
        private void CodeLength(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.CodeLength = source.GetInt32();
        }
        private void DescriptionLength(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.DescriptionLength = source.GetInt32();
        }
        private void UseAutoOrder(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.UseAutoOrder = (source.GetInt32() == 1);
        }
        private void AutoOrderLength(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.AutoOrderLength = source.GetInt32();
        }
        private void DimensionTypes(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.DimensionTypes = source.GetUuid();
        }
        private void MaxDimensionCount(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.MaxDimensionCount = source.GetInt32();
        }
        private void PropertyCollection(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (source.Token == TokenType.StartObject)
            {
                _propertyParser.Parse(in source, out List<MetadataProperty> properties);

                if (properties is not null && properties.Count > 0)
                {
                    _target.Properties.AddRange(properties);
                }
            }
        }
        private void TablePartCollection(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (source.Token == TokenType.StartObject)
            {
                _tableParser.Parse(in source, out List<TablePart> tables);

                if (tables is not null && tables.Count > 0)
                {
                    _target.TableParts = tables;
                }
            }
        }
    }
}