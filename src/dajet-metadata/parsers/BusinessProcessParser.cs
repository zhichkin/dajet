using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class BusinessProcessParser : IMetadataObjectParser
    {
        private readonly OneDbMetadataProvider _cache;
        private ConfigFileParser _parser;
        private TablePartCollectionParser _tableParser;
        private MetadataPropertyCollectionParser _propertyParser;

        private MetadataInfo _entry;
        private BusinessProcess _target;
        private ConfigFileConverter _converter;
        public BusinessProcessParser() { }
        public BusinessProcessParser(OneDbMetadataProvider cache) { _cache = cache; }
        public void Parse(in ConfigFileOptions options, out MetadataInfo info)
        {
            _entry = new MetadataInfo()
            {
                MetadataUuid = options.MetadataUuid,
                MetadataType = MetadataTypes.BusinessProcess
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.1.1.2 - uuid объекта метаданных (FileName)

            _converter[1][5] += Reference; // Идентификатор ссылочного типа данных, например, "БизнесПроцессСсылка.Согласование"
            _converter[1][1][2] += Name; // Имя объекта метаданных конфигурации
            _converter[1][25] += BusinessTask; // Ссылка на объект метаданных "Задача", используемый бизнес-процессом

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
                MetadataType = MetadataTypes.BusinessProcess
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.1.1.2 - uuid объекта метаданных (FileName)

            _converter[1][5] += Reference; // Идентификатор ссылочного типа данных, например, "БизнесПроцессСсылка.Согласование"
            _converter[1][1][2] += Name; // Имя объекта метаданных конфигурации
            _converter[1][25] += BusinessTask; // Ссылка на объект метаданных "Задача", используемый бизнес-процессом

            _parser.Parse(in source, in _converter);

            target = _entry;

            _entry = null;
            _parser = null;
            _converter = null;
        }
        public void Parse(in ConfigFileReader reader, Guid uuid, out MetadataObject target)
        {
            _target = new BusinessProcess() { Uuid = uuid };

            ConfigureConverter();

            _parser = new ConfigFileParser();
            _tableParser = new TablePartCollectionParser(_cache);
            _propertyParser = new MetadataPropertyCollectionParser(_cache);

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

            if (_cache is not null && _cache.Extension is not null) // 1.1.8 = 0 если заимствование отстутствует
            {
                _converter[1][1][9] += Parent; // uuid расширяемого объекта метаданных
            }

            _converter[1][1][2] += Name;
            _converter[1][1][3][2] += Alias;
            _converter[1][16] += NumberType;
            _converter[1][17] += Periodicity;
            _converter[1][18] += NumberLength;
            _converter[1][25] += BusinessTask; // Ссылка на объект метаданных "Задача", используемый бизнес-процессом

            _converter[6] += PropertyCollection; // 87c988de-ecbf-413b-87b0-b9516df05e28 - идентификатор коллекции реквизитов
            _converter[7] += TablePartCollection; // a3fe6537-d787-40f7-8a06-419d2f0c1cfd - идентификатор коллекции табличных частей
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
        private void NumberType(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.NumberType = (NumberType)source.GetInt32();
        }
        private void NumberLength(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.NumberLength = source.GetInt32();
        }
        private void Periodicity(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.Periodicity = (Periodicity)source.GetInt32();
        }
        private void Reference(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (_entry is not null)
            {
                _entry.ReferenceUuid = source.GetUuid();
            }
        }
        private void BusinessTask(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (_entry is not null)
            {
                _entry.BusinessTask = source.GetUuid();
            }
            else if (_target is not null)
            {
                _target.BusinessTask = source.GetUuid();
            }
        }
        private void PropertyCollection(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (source.Token == TokenType.StartObject)
            {
                _propertyParser.Parse(in source, out List<MetadataProperty> properties);

                if (properties is not null && properties.Count > 0)
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

                if (tables is not null && tables.Count > 0)
                {
                    _target.TableParts = tables;
                }
            }
        }
        private void Parent(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (_entry is not null)
            {
                _entry.MetadataParent = source.GetUuid();
            }
            else if (_target is not null)
            {
                _target.Parent = source.GetUuid();
            }
        }
    }
}