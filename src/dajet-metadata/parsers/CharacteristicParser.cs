using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class CharacteristicParser : IMetadataObjectParser
    {
        private readonly MetadataCache _cache;
        private ConfigFileParser _parser;
        private DataTypeSetParser _typeParser;
        private TablePartCollectionParser _tableParser;
        private MetadataPropertyCollectionParser _propertyParser;

        private MetadataInfo _entry;
        private Characteristic _target;
        private ConfigFileConverter _converter;
        public CharacteristicParser(MetadataCache cache)
        {
            _cache = cache;
        }
        public void Parse(in ConfigFileReader source, out MetadataInfo target)
        {
            _entry = new MetadataInfo()
            {
                MetadataType = MetadataTypes.Characteristic,
                MetadataUuid = new Guid(source.FileName)
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            _converter[1][3] += Reference; // ПланВидовХарактеристикСсылка
            _converter[1][9] += CharacteristicUuid; // Идентификатор характеристики
            _converter[1][13][1][2] += Name; // Имя объекта конфигурации

            _parser.Parse(in source, in _converter);

            target = _entry;

            _entry = null;
            _parser = null;
            _converter = null;
        }
        public void Parse(in ConfigFileReader source, out MetadataObject target)
        {
            _target = new Characteristic()
            {
                Uuid = new Guid(source.FileName)
            };

            ConfigureConverter();

            _parser = new ConfigFileParser();
            _typeParser = new DataTypeSetParser(_cache);
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

            _converter[1][13][1][2] += Name;
            _converter[1][13][1][3][2] += Alias;
            _converter[1][18] += DataTypeSet;
            _converter[1][19] += IsHierarchical;
            _converter[1][21] += CodeLength;
            _converter[1][23] += DescriptionLength;

            _converter[3] += PropertyCollection; // 31182525-9346-4595-81f8-6f91a72ebe06 - идентификатор коллекции реквизитов
            _converter[5] += TablePartCollection; // 54e36536-7863-42fd-bea3-c5edd3122fdc - идентификатор коллекции табличных частей
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
                
                args.Cancel = true;
                
                return;
            }

            if (_target != null)
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
        private void DataTypeSet(in ConfigFileReader source, in CancelEventArgs args)
        {
            // Корневой узел объекта "ОписаниеТипов"

            if (source.Token == TokenType.StartObject)
            {
                _typeParser.Parse(in source, out DataTypeSet type);

                _target.DataTypeSet = type;
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
    }
}