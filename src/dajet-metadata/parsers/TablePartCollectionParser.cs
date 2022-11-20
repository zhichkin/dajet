using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System.Collections.Generic;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class TablePartCollectionParser
    {
        private readonly MetadataCache _cache;
        private ConfigFileParser _parser;
        private ConfigFileConverter _converter;
        private MetadataPropertyCollectionParser _propertyParser;
        private TablePart _tablePart;
        private List<TablePart> _target;
        public TablePartCollectionParser(MetadataCache cache)
        {
            _cache = cache;
        }
        public void Parse(in ConfigFileReader reader, out List<TablePart> target)
        {
            ConfigureCollectionConverter(in reader);

            _parser = new ConfigFileParser();
            _propertyParser = new MetadataPropertyCollectionParser(_cache);

            _target = new List<TablePart>();

            _parser.Parse(in reader, in _converter);

            // result
            target = _target;
            
            // dispose private variables
            _target = null;
            _tablePart = null;
            _parser = null;
            _converter = null;
            _propertyParser = null;
        }
        private void ConfigureCollectionConverter(in ConfigFileReader reader)
        {
            _converter = new ConfigFileConverter();

            // Параметр reader должен быть позиционирован в данный момент
            // на узле коллекции свойств объекта метаданных (токен = '{')
            // reader.Char == '{' && reader.Token == TokenType.StartObject
            _converter = _converter.Path(reader.Level - 1, reader.Path);

            // Необходимо прекратить чтение коллекции,
            // чтобы позволить другим парсерам выполнить свою работу,
            // по чтению потока байт source (данный парсер является вложенным)
            _converter += Cancel;

            // Свойства типизированной коллекции
            //_converter[0] += Uuid; // идентификатор (параметр типа коллекции - тип данных элементов коллекции)
            _converter[1] += Count; // количество элементов в коллекции

            // Объекты элементов коллекции, в зависимости от значения _converter[1],
            // располагаются в коллекции последовательно по адресам _converter[2..N]
        }
        private void Count(in ConfigFileReader reader, in CancelEventArgs args)
        {
            int count = reader.GetInt32(); // количество табличных частей

            int offset = 2; // начальный индекс N

            for (int n = 0; n < count; n++)
            {
                _converter[offset + n] += TablePartConverter;
            }
        }
        private void Cancel(in ConfigFileReader reader, in CancelEventArgs args)
        {
            if (reader.Token == TokenType.EndObject)
            {
                args.Cancel = true;
            }
        }
        private void TablePartConverter(in ConfigFileReader reader, in CancelEventArgs args)
        {
            // начало объекта "ТабличнаяЧасть"
            if (reader.Token == TokenType.StartObject)
            {
                _converter = _converter.Path(reader.Level - 1, reader.Path); // корневой узел

                _tablePart = new TablePart();

                //TODO: extensions support (!)
                // [5][2] 0.1.5.1.8 - флаг заимствования объекта из основной конфигурации ??? 0 если заимствование отстутствует
                // [5][2] 0.1.5.1.9 - uuid расширяемого объекта метаданных

                _converter[0][1][5][1][1][2] += Uuid;
                _converter[0][1][5][1][2] += Name;
                
                _converter[2] += PropertyCollection;
            }

            // конец объекта "ТабличнаяЧасть"
            if (reader.Token == TokenType.EndObject)
            {
                _target.Add(_tablePart);
            }
        }
        private void Uuid(in ConfigFileReader source, in CancelEventArgs args)
        {
            _tablePart.Uuid = source.GetUuid();
        }
        private void Name(in ConfigFileReader source, in CancelEventArgs args)
        {
            _tablePart.Name = source.Value;
        }
        private void PropertyCollection(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (source.Token == TokenType.StartObject)
            {
                _propertyParser.Parse(in source, out List<MetadataProperty> properties);

                if (properties != null && properties.Count > 0)
                {
                    _tablePart.Properties = properties;
                }
            }
        }
    }
}