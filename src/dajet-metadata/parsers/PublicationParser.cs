using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class PublicationParser : IMetadataObjectParser
    {
        private readonly MetadataCache _cache;
        private ConfigFileParser _parser;
        private TablePartCollectionParser _tableParser;
        private MetadataPropertyCollectionParser _propertyParser;

        private Publication _target;
        private MetadataInfo _entry;
        private ConfigFileConverter _converter;
        public PublicationParser() { }
        public PublicationParser(MetadataCache cache) { _cache = cache; }
        public void Parse(in ConfigFileOptions options, out MetadataInfo info)
        {
            _entry = new MetadataInfo()
            {
                MetadataUuid = options.MetadataUuid,
                MetadataType = MetadataTypes.Publication
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.12.1.2 - uuid объекта метаданных (FileName)

            _converter[1][3] += Reference; // Идентификатор ссылочного типа данных
            _converter[1][12][2] += Name;  // Имя объекта конфигурации

            if (options.IsExtension)
            {
                _converter[1][12][9] += Parent;
            }

            _converter[1][12] += Cancel;

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
                MetadataType = MetadataTypes.Publication
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.12.1.2 - uuid объекта метаданных (FileName)

            _converter[1][3] += Reference; // Идентификатор ссылочного типа данных
            _converter[1][12][2] += Name;  // Имя объекта конфигурации
            _converter[1][12] += Cancel;

            _parser.Parse(in source, in _converter);

            target = _entry;

            _entry = null;
            _parser = null;
            _converter = null;
        }
        public void Parse(in ConfigFileReader reader, Guid uuid, out MetadataObject target)
        {
            _target = new Publication() { Uuid = uuid };

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

            if (_cache != null && _cache.Extension != null) // 1.12.8 = 0 если заимствование отстутствует
            {
                _converter[1][12][9] += Parent; // uuid расширяемого объекта метаданных
            }

            _converter[1][12][2] += Name;
            _converter[1][12][3][2] += Alias;
            _converter[1][15] += CodeLength;
            _converter[1][17] += DescriptionLength;
            _converter[1][26] += IsDistributed;

            _converter[3] += PropertyCollection;
            _converter[5] += TablePartCollection;

            if (_cache != null)
            {
                _converter[4] += TemplateCollection;
            }
        }
        private void Cancel(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (source.Token == TokenType.EndObject)
            {
                args.Cancel = true;
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
        private void Reference(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (_entry != null)
            {
                _entry.ReferenceUuid = source.GetUuid();
            }
        }
        private void CodeLength(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.CodeLength = source.GetInt32();
        }
        private void DescriptionLength(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.DescriptionLength = source.GetInt32();
        }
        private void IsDistributed(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.IsDistributed = (source.GetInt32() != 0);
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
        private void TemplateCollection(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (source.Token == TokenType.StartObject)
            {
                new TemplateCollectionParser(_cache).Parse(in source, out List<Template> templates);

                if (templates != null && templates.Count > 0)
                {
                    _target.Templates = templates;
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