using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class InformationRegisterParser : IMetadataObjectParser
    {
        private readonly MetadataCache _cache;
        private ConfigFileParser _parser;
        private MetadataPropertyCollectionParser _propertyParser;

        private MetadataInfo _entry;
        private InformationRegister _target;
        private ConfigFileConverter _converter;
        public InformationRegisterParser() { }
        public InformationRegisterParser(MetadataCache cache) { _cache = cache; }
        public void Parse(in ConfigFileOptions options, out MetadataInfo info)
        {
            _entry = new MetadataInfo()
            {
                MetadataUuid = options.MetadataUuid,
                MetadataType = MetadataTypes.InformationRegister
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.15.1.1.2 - uuid объекта метаданных (FileName)

            _converter[1][15][1][2] += Name; // Имя объекта метаданных конфигурации

            if (options.IsExtension)
            {
                _converter[1][15][1][13] += Parent;
            }

            _converter[1][15][1] += Cancel;

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
                MetadataType = MetadataTypes.InformationRegister
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.15.1.1.2 - uuid объекта метаданных (FileName)

            _converter[1][15][1][2] += Name; // Имя объекта метаданных конфигурации

            _parser.Parse(in source, in _converter);

            target = _entry;

            _entry = null;
            _parser = null;
            _converter = null;
        }
        public void Parse(in ConfigFileReader reader, Guid uuid, out MetadataObject target)
        {
            _target = new InformationRegister() { Uuid = uuid };

            ConfigureConverter();

            _parser = new ConfigFileParser();
            _propertyParser = new MetadataPropertyCollectionParser(_cache, _target);

            _parser.Parse(in reader, in _converter);

            // result
            target = _target;

            // dispose private variables
            _target = null;
            _parser = null;
            _converter = null;
            _propertyParser = null;
        }
        private void ConfigureConverter()
        {
            _converter = new ConfigFileConverter();

            if (_cache != null && _cache.Extension != null) // 1.15.1.8 = 0 если заимствование отстутствует
            {
                _converter[1][15][1][13] += Parent; // uuid расширяемого объекта метаданных
            }

            _converter[1][15][1][2] += Name;
            _converter[1][15][1][3][2] += Alias;
            _converter[1][18] += Periodicity;
            _converter[1][19] += UseRecorder;

            ConfigurePropertyConverters();
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
        private void Periodicity(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.Periodicity = (RegisterPeriodicity)source.GetInt32();
        }
        private void UseRecorder(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.UseRecorder = (source.GetInt32() != 0);
        }
        private void ConfigurePropertyConverters()
        {
            // коллекции свойств регистра сведений
            _converter[3] += PropertyCollection; // ресурсы
            _converter[4] += PropertyCollection; // измерения
            _converter[7] += PropertyCollection; // реквизиты
        }
        private void PropertyCollection(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (source.Token == TokenType.StartObject)
            {
                _propertyParser.Parse(in source, out List<MetadataProperty> properties);

                if (properties != null && properties.Count > 0)
                {
                    _target.Properties.AddRange(properties);
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