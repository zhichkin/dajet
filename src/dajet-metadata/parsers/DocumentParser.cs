using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class DocumentParser : IMetadataObjectParser
    {
        private readonly MetadataCache _cache;
        private ConfigFileParser _parser;
        private TablePartCollectionParser _tableParser;
        private MetadataPropertyCollectionParser _propertyParser;

        private Document _target;
        private MetadataInfo _entry;
        private ConfigFileConverter _converter;
        public DocumentParser(MetadataCache cache)
        {
            _cache = cache;
        }
        public void Parse(in ConfigFileReader source, Guid uuid, out MetadataInfo target)
        {
            _entry = new MetadataInfo()
            {
                MetadataUuid = uuid,
                MetadataType = MetadataTypes.Document
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.9.1.1.2 - uuid объекта метаданных (FileName)

            _converter[1][3] += Reference; // ДокументСсылка
            _converter[1][9][1][2] += Name; // Имя объекта метаданных конфигурации
            _converter[1][24] += Registers; // Коллекция регистров движения документа

            _parser.Parse(in source, in _converter);

            target = _entry;

            _entry = null;
            _parser = null;
            _converter = null;
        }
        public void Parse(in ConfigFileReader reader, Guid uuid, out MetadataObject target)
        {
            _target = new Document() { Uuid = uuid };

            ConfigureConverter();

            _parser = new ConfigFileParser();
            _tableParser = new TablePartCollectionParser(_cache);
            _propertyParser = new MetadataPropertyCollectionParser(_cache);

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

            if (_cache.Extension != null) // 1.9.1.8 = 0 если заимствование отстутствует
            {
                _converter[1][9][1][9] += Parent; // uuid расширяемого объекта метаданных
            }

            _converter[1][9][1][2] += Name;
            _converter[1][9][1][3][2] += Alias;
            _converter[1][11] += NumberType;
            _converter[1][12] += NumberLength;
            _converter[1][13] += Periodicity;

            _converter[3] += TablePartCollection;
            _converter[5] += PropertyCollection;
        }
        private void Name(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (_entry != null)
            {
                _entry.Name = source.Value;
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
            if (_entry != null)
            {
                _entry.ReferenceUuid = source.GetUuid();
            }
        }
        private void Registers(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (source.Token == TokenType.EndObject)
            {
                args.Cancel = true;

                return;
            }

            // 1.24.0 - UUID коллекции регистров движения !?
            // 1.24.1 - количество регистров движения
            // 1.24.N - описание регистров движения
            // 1.24.N.2.1 - uuid'ы регистров движения (file names)

            _ = source.Read(); // [1][24][0] - UUID коллекции регистров движения
            _ = source.Read(); // [1][24][1] - количество регистров движения

            int count = source.GetInt32();

            if (count == 0)
            {
                return;
            }

            int offset = 2; // начальный индекс N [1][24][2]

            for (int n = 0; n < count; n++)
            {
                _converter[1][24][offset + n][2][1] += RegisterUuid;
            }
        }
        private void RegisterUuid(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (_entry != null)
            {
                _entry.DocumentRegisters.Add(source.GetUuid());
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
            _target.Parent = source.GetUuid();
        }
    }
}