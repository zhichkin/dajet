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
        public InformationRegisterParser(MetadataCache cache)
        {
            _cache = cache;
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

            //TODO: extensions support (!)
            // 1.15.1.1.2 - uuid объекта метаданных (FileName)
            // 1.15.1.8 - флаг заимствования объекта из основной конфигурации ??? 0 если заимствование отстутствует
            // 1.15.1.13 - uuid расширяемого объекта метаданных

            _converter[1][15][1][2] += Name;
            _converter[1][15][1][3][2] += Alias;
            _converter[1][18] += Periodicity;
            _converter[1][19] += UseRecorder;

            ConfigurePropertyConverters();
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
    }
}