using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class BusinessTaskParser : IMetadataObjectParser
    {
        private readonly OneDbMetadataProvider _cache;
        private ConfigFileParser _parser;
        private TablePartCollectionParser _tableParser;
        private MetadataPropertyCollectionParser _propertyParser;

        private MetadataInfo _entry;
        private BusinessTask _target;
        private ConfigFileConverter _converter;
        public BusinessTaskParser() { }
        public BusinessTaskParser(OneDbMetadataProvider cache) { _cache = cache; }
        public void Parse(in ConfigFileOptions options, out MetadataInfo info)
        {
            _entry = new MetadataInfo()
            {
                MetadataUuid = options.MetadataUuid,
                MetadataType = MetadataTypes.BusinessTask
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.1.1.2 - uuid объекта метаданных (FileName)

            _converter[1][1][2] += Name; // Имя объекта метаданных конфигурации
            _converter[1][5] += Reference; // Идентификатор ссылочного типа данных, например, "ЗадачаСсылка.Задача"
            
            if (options.IsExtension)
            {
                _converter[1][1][9] += Parent;
            }

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
                MetadataType = MetadataTypes.BusinessTask
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.1.1.2 - uuid объекта метаданных (FileName)

            _converter[1][1][2] += Name; // Имя объекта метаданных конфигурации
            _converter[1][5] += Reference; // Идентификатор ссылочного типа данных, например, "ЗадачаСсылка.Задача"
            
            _parser.Parse(in source, in _converter);

            target = _entry;

            _entry = null;
            _parser = null;
            _converter = null;
        }
        public void Parse(in ConfigFileReader reader, Guid uuid, out MetadataObject target)
        {
            _target = new BusinessTask() { Uuid = uuid };

            ConfigureConverter();

            _parser = new ConfigFileParser();
            _tableParser = new TablePartCollectionParser(_cache);
            _propertyParser = new MetadataPropertyCollectionParser(_cache, _target);

            _parser.Parse(in reader, in _converter);

            target = _target; // result

            // disposing
            _target = null;
            _parser = null;
            _converter = null;
            _tableParser = null;
            _propertyParser = null;
        }
        private void ConfigureConverter()
        {
            _converter = new ConfigFileConverter();


            if (_cache != null && _cache.Extension != null) // 1.1.8 = 0 если заимствование отстутствует
            {
                _converter[1][1][9] += Parent; // uuid расширяемого объекта метаданных
            }

            _converter[1][1][2] += Name;
            _converter[1][1][3][2] += Alias;
            _converter[1][18] += NumberType;
            _converter[1][19] += NumberLength;
            _converter[1][22] += DescriptionLength;
            _converter[1][25] += RoutingTable; // Идентификатор регистра сведений, используемого для адресации задачи
            _converter[1][26] += MainRoutingProperty; // Основной реквизит адресации задачи

            _converter[5] += PropertyCollection; // 8ddfb495-c5fc-46b9-bdc5-bcf58341bff0 - идентификатор коллекции реквизитов
            _converter[6] += PropertyCollection; // e97c0570-251c-4566-b0f1-10686820f143- идентификатор коллекции реквизитов адресации
            _converter[7] += TablePartCollection; // ee865d4b-a458-48a0-b38f-5a26898feeb0 - идентификатор коллекции табличных частей

        }
        private void Reference(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (_entry != null)
            {
                _entry.ReferenceUuid = source.GetUuid();
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
        private void NumberType(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.NumberType = (NumberType)source.GetInt32();
        }
        private void NumberLength(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.NumberLength = source.GetInt32();
        }
        private void DescriptionLength(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.DescriptionLength = source.GetInt32();
        }
        private void RoutingTable(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.RoutingTable = source.GetUuid();
        }
        private void MainRoutingProperty(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.MainRoutingProperty = source.GetUuid();
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
    }
}