using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class CharacteristicParser : IMetadataObjectParser
    {
        private readonly OneDbMetadataProvider _cache;
        private ConfigFileParser _parser;
        private DataTypeDescriptorParser _typeParser;
        private TablePartCollectionParser _tableParser;
        private MetadataPropertyCollectionParser _propertyParser;

        private MetadataInfo _entry;
        private Characteristic _target;
        private ConfigFileConverter _converter;
        public CharacteristicParser() { }
        public CharacteristicParser(OneDbMetadataProvider cache) { _cache = cache; }
        public void Parse(in ConfigFileOptions options, out MetadataInfo info)
        {
            _entry = new MetadataInfo()
            {
                MetadataUuid = options.MetadataUuid,
                MetadataType = MetadataTypes.Characteristic
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.13.1.1.2 - uuid объекта метаданных (FileName)

            _converter[1][3] += Reference; // ПланВидовХарактеристикСсылка
            _converter[1][9] += CharacteristicUuid; // Идентификатор характеристики
            _converter[1][13][1][2] += Name; // Имя объекта конфигурации

            if (options.IsExtension)
            {
                _converter[1][13][1][11] += Parent;
            }
            
            _converter[1][13][1] += Cancel; // Прервать чтение файла

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
                MetadataType = MetadataTypes.Characteristic
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();
            _typeParser = new DataTypeDescriptorParser(_cache);

            // 1.13.1.1.2 - uuid объекта метаданных (FileName)

            _converter[1][3] += Reference; // ПланВидовХарактеристикСсылка
            _converter[1][9] += CharacteristicUuid; // Идентификатор характеристики
            _converter[1][13][1][2] += Name; // Имя объекта конфигурации
            _converter[1][18] += DataTypeDescriptor; // "ОписаниеТипов" типов значений характеристик
            _converter[1][18] += Cancel; // Прервать чтение файла

            _parser.Parse(in source, in _converter);

            target = _entry;

            _entry = null;
            _parser = null;
            _converter = null;
            _typeParser = null;
        }
        public void Parse(in ConfigFileReader source, Guid uuid, out MetadataObject target)
        {
            _target = new Characteristic() { Uuid = uuid };

            ConfigureConverter();

            _parser = new ConfigFileParser();
            _typeParser = new DataTypeDescriptorParser(_cache);
            _tableParser = new TablePartCollectionParser(_cache);
            _propertyParser = new MetadataPropertyCollectionParser(_cache, _target);

            _parser.Parse(in source, in _converter);

            // result
            target = _target;

            // dispose private variables
            _target = null;
            _parser = null;
            _converter = null;
            _typeParser = null;
            _tableParser = null;
            _propertyParser = null;
        }
        private void ConfigureConverter()
        {
            _converter = new ConfigFileConverter();

            if (_cache != null && _cache.Extension != null) // 1.13.1.8 = 0 если заимствование отстутствует
            {
                _converter[1][13][1][11] += Parent; // uuid расширяемого объекта метаданных

                //FIXME: extensions support (!)
                // [1][13][1][15] - Объект описания дополнительных типов данных определяемого типа
                // [1][13][1][15][0] = #
                // [1][13][1][15][1] = f5c65050-3bbb-11d5-b988-0050bae0a95d (константа)
                // [1][13][1][15][2] = {объект описания типов данных - Pattern} аналогично [1][18] += DataTypeDescriptor

                _converter[1][13][1][15][2] += ExtensionDataTypeDescriptor;
            }

            _converter[1][13][1][2] += Name;
            _converter[1][13][1][3][2] += Alias;
            _converter[1][18] += DataTypeDescriptor;
            _converter[1][19] += IsHierarchical;
            _converter[1][21] += CodeLength;
            _converter[1][23] += DescriptionLength;

            _converter[3] += PropertyCollection; // 31182525-9346-4595-81f8-6f91a72ebe06 - идентификатор коллекции реквизитов
            _converter[5] += TablePartCollection; // 54e36536-7863-42fd-bea3-c5edd3122fdc - идентификатор коллекции табличных частей
        }
        private void Cancel(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (source.Token == TokenType.EndObject)
            {
                args.Cancel = true;
            }
        }
        private void Reference(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (_entry != null)
            {
                _entry.ReferenceUuid = source.GetUuid();
            }
        }
        private void CharacteristicUuid(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (_entry != null)
            {
                _entry.CharacteristicUuid = source.GetUuid();
            }
        }
        private void Name(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (_entry != null)
            {
                _entry.Name = source.Value;
            }
            else if (_target != null)
            {
                _target.Name = source.Value;
            }
        }
        private void Alias(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.Alias = source.Value;
        }
        private void CodeLength(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.CodeLength = source.GetInt32();
        }
        private void DescriptionLength(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.DescriptionLength = source.GetInt32();
        }
        private void IsHierarchical(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.IsHierarchical = (source.GetInt32() != 0);
        }
        private void DataTypeDescriptor(in ConfigFileReader source, in CancelEventArgs args)
        {
            // Корневой узел объекта "ОписаниеТипов"

            if (source.Token == TokenType.StartObject)
            {
                _typeParser.Parse(in source, out DataTypeDescriptor type);

                if (_entry is not null) // Первичная загрузка метаданных в кэш
                {
                    _entry.DataTypeDescriptor = type;
                }
                else // Полная загрузка объекта метаданных
                {
                    _target.DataTypeDescriptor = type;
                }

                //FIXME: extension has higher priority
                //type.Merge(_target.DataTypeDescriptor);
                //_target.DataTypeDescriptor = type;
            }
        }
        private void PropertyCollection(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (source.Token == TokenType.StartObject)
            {
                _propertyParser.Parse(in source, out List<MetadataProperty> properties);

                if (properties != null && properties.Count > 0)
                {
                    _target.Properties = properties;
                }
            }
        }
        private void TablePartCollection(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (source.Token == TokenType.StartObject)
            {
                _tableParser.Parse(in source, out List<TablePart> tables);

                if (tables != null && tables.Count > 0)
                {
                    _target.TableParts = tables;
                }
            }
        }
        private void Parent(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (_entry != null)
            {
                _entry.MetadataParent = source.GetUuid();
            }
            else if (_target != null)
            {
                _target.Parent = source.GetUuid();
            }
        }
        private void ExtensionDataTypeDescriptor(in ConfigFileReader source, in CancelEventArgs args)
        {
            // Корневой узел объекта "ОписаниеТипов"

            if (source.Token != TokenType.StartObject)
            {
                return;
            }

            _typeParser.Parse(in source, out DataTypeDescriptor type);

            _target.ExtensionDataTypeDescriptor = type;

            //FIXME: extension has higher priority
            //_target.DataTypeDescriptor.Merge(in type);
        }
    }
}