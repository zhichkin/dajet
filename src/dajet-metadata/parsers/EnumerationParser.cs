using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class EnumerationParser : IMetadataObjectParser
    {
        private readonly MetadataCache _cache;
        private ConfigFileParser _parser;

        private Enumeration _target;
        private MetadataInfo _entry;
        private ConfigFileConverter _converter;
        private int _count; // количество значений перечисления
        private EnumValue _value;
        public EnumerationParser(MetadataCache cache)
        {
            _cache = cache;
        }
        public void Parse(in ConfigFileReader source, out MetadataInfo target)
        {
            _entry = new MetadataInfo()
            {
                MetadataType = MetadataTypes.Enumeration,
                MetadataUuid = new Guid(source.FileName)
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            _converter[1][1] += Reference; // Идентификатор ссылочного типа данных
            _converter[1][5][1][2] += Name; // Имя объекта конфигурации

            _parser.Parse(in source, in _converter);

            target = _entry;

            _entry = null;
            _parser = null;
            _converter = null;
        }
        public void Parse(in ConfigFileReader reader, out MetadataObject target)
        {
            _target = new Enumeration()
            {
                Uuid = new Guid(reader.FileName)
            };

            ConfigureConverter();

            _parser = new ConfigFileParser();

            _parser.Parse(in reader, in _converter);

            // result
            target = _target;

            // dispose private variables
            _target = null;
            _parser = null;
            _converter = null;
        }
        private void ConfigureConverter()
        {
            _converter = new ConfigFileConverter();

            _converter[1][5][1][2] += Name;
            _converter[1][5][1][3][2] += Alias;
            _converter[6] += EnumerationValues;
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
        private void Reference(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (_entry != null)
            {
                _entry.ReferenceUuid = source.GetUuid();
            }
        }
        private void EnumerationValues(in ConfigFileReader source, in CancelEventArgs args)
        {
            _converter[6][0] += Uuid;
            _converter[6][1] += Count;
        }
        ///<summary>Идентификатор коллекции значений перечисления <see cref="SystemUuid.Enumeration_Values"/></summary>
        private void Uuid(in ConfigFileReader source, in CancelEventArgs args)
        {
            // Guid type = source.GetUuid();
        }
        private void Count(in ConfigFileReader source, in CancelEventArgs args)
        {
            _count = source.GetInt32(); // [6][1] количество значений перечисления

            _converter = _converter[6]; // корневой узел коллекции

            int offset = 2; // начальный индекс для узлов элементов коллекции от её корня

            for (int n = 0; n < _count; n++)
            {
                _converter[offset + n] += ValueConverter;
            }
        }
        private void ValueConverter(in ConfigFileReader source, in CancelEventArgs args)
        {
            // начало чтения объекта свойства
            if (source.Token == TokenType.StartObject)
            {
                // корневой узел объекта свойства
                _converter = _converter.Path(source.Level - 1, source.Path);

                _value = new EnumValue();

                _converter[0][1][1][2] += ValueUuid;
                _converter[0][1][2] += ValueName;
                _converter[0][1][3][2] += ValueAlias;
            }

            // завершение чтения объекта свойства
            if (source.Token == TokenType.EndObject)
            {
                _target.Values.Add(_value);
            }
        }
        private void ValueUuid(in ConfigFileReader source, in CancelEventArgs args)
        {
            _value.Uuid = source.GetUuid();
        }
        private void ValueName(in ConfigFileReader source, in CancelEventArgs args)
        {
            _value.Name = source.Value;
        }
        private void ValueAlias(in ConfigFileReader source, in CancelEventArgs args)
        {
            _value.Alias = source.Value;
        }
    }
}