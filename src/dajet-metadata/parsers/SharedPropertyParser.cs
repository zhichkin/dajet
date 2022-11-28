using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class SharedPropertyParser : IMetadataObjectParser
    {
        private readonly MetadataCache _cache;
        private ConfigFileParser _parser;
        private DataTypeSetParser _typeParser;

        int _count = 0;
        private MetadataInfo _entry;
        private SharedProperty _target;
        private ConfigFileConverter _converter;
        public SharedPropertyParser(MetadataCache cache)
        {
            _cache = cache;
        }
        public void Parse(in ConfigFileReader source, Guid uuid, out MetadataInfo target)
        {
            _entry = new MetadataInfo()
            {
                MetadataUuid = uuid,
                MetadataType = MetadataTypes.SharedProperty
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            _converter[1][1][1][1][2] += Name; // Имя объекта конфигурации

            _parser.Parse(in source, in _converter);

            target = _entry;

            _entry = null;
            _parser = null;
            _converter = null;
        }
        public void Parse(in ConfigFileReader source, Guid uuid, out MetadataObject target)
        {
            _target = new SharedProperty() { Uuid = uuid };

            ConfigureConverter();

            _typeParser = new DataTypeSetParser(_cache);

            _parser = new ConfigFileParser();
            _parser.Parse(in source, in _converter);

            // result
            target = _target;

            // dispose private variables
            _count = 0;
            _target = null;
            _parser = null;
            _converter = null;
            _typeParser = null;
        }
        private void ConfigureConverter()
        {
            _converter = new ConfigFileConverter();

            _converter[1][1][1][1][2] += Name;
            _converter[1][1][1][1][3][2] += Alias;
            _converter[1][6] += AutomaticUsage;
            _converter[1][2][1] += UsageSettings; // количество объектов метаданных, у которых значение использования общего реквизита не равно "Автоматически"
            _converter[1][1][1][2] += PropertyType; // описание допустимых типов данных (объект)
            _converter[1][5] += DataSeparationUsage;
            _converter[1][12] += DataSeparationMode;
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
        private void AutomaticUsage(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.AutomaticUsage = (AutomaticUsage)source.GetInt32();
        }
        private void UsageSettings(in ConfigFileReader source, in CancelEventArgs args)
        {
            _count = source.GetInt32(); // количество настроек использования общего реквизита

            Guid uuid; // file name объекта метаданных, для которого используется настройка
            int usage; // значение настройки использования общего реквизита объектом метаданных

            while (_count > 0)
            {
                _ = source.Read(); // [2] (1.2.2) 0221aa25-8e8c-433b-8f5b-2d7fead34f7a
                uuid = source.GetUuid(); // file name объекта метаданных
                if (uuid == Guid.Empty) { throw new FormatException(); }

                _ = source.Read(); // [2] (1.2.3) { Начало объекта настройки
                _ = source.Read(); // [3] (1.2.3.0) 2
                _ = source.Read(); // [3] (1.2.3.1) 1
                usage = source.GetInt32(); // настройка использования общего реквизита
                if (usage == -1) { throw new FormatException(); }
                _ = source.Read(); // [3] (1.2.3.2) 00000000-0000-0000-0000-000000000000
                _ = source.Read(); // [2] (1.2.3) } Конец объекта настройки

                _target.UsageSettings.Add(uuid, (SharedPropertyUsage)usage);

                _count--; // Конец чтения настройки для объекта метаданных
            }
        }
        private void DataSeparationMode(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.DataSeparationMode = (DataSeparationMode)source.GetInt32();
        }
        private void DataSeparationUsage(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.DataSeparationUsage = (DataSeparationUsage)source.GetInt32();
        }
        private void PropertyType(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (source.Token == TokenType.EndObject)
            {
                return;
            }

            _typeParser.Parse(in source, out DataTypeSet type);

            _target.PropertyType = type;
        }
    }
}