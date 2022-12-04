using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class NamedDataTypeSetParser : IMetadataObjectParser
    {
        private readonly MetadataCache _cache;
        private ConfigFileParser _parser;
        private DataTypeSetParser _typeParser;

        private MetadataInfo _entry;
        private NamedDataTypeSet _target;
        private ConfigFileConverter _converter;
        public NamedDataTypeSetParser(MetadataCache cache) { _cache = cache; }
        public void Parse(in ConfigFileReader source, Guid uuid, out MetadataInfo target)
        {
            _entry = new MetadataInfo()
            {
                MetadataUuid = uuid,
                MetadataType = MetadataTypes.NamedDataTypeSet
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.3.1.2 - uuid объекта метаданных (FileName)

            _converter[1][1] += Reference; // Идентификатор ссылочного типа данных "Ссылка"
            _converter[1][3][2] += Name; // Имя объекта конфигурации

            _parser.Parse(in source, in _converter);

            target = _entry;

            _entry = null;
            _parser = null;
            _converter = null;
        }
        public void Parse(in ConfigFileReader source, Guid uuid, out MetadataObject target)
        {
            _target = new NamedDataTypeSet() { Uuid = uuid };
            
            ConfigureConverter();

            _typeParser = new DataTypeSetParser(_cache);

            _parser = new ConfigFileParser();
            _parser.Parse(in source, in _converter);

            // result
            target = _target;

            // dispose private variables
            _target = null;
            _parser = null;
            _converter = null;
            _typeParser = null;
        }
        private void ConfigureConverter()
        {
            _converter = new ConfigFileConverter();

            if (_cache.Extension != null) // 1.3.8 = 0 если заимствование отстутствует
            {
                _converter[1][3][11] += Parent; // uuid расширяемого объекта метаданных

                //FIXME: extensions support (!)
                // [1][3][15] - Объект описания дополнительных типов данных определяемого типа
                // [1][3][15][0] = #
                // [1][3][15][1] = f5c65050-3bbb-11d5-b988-0050bae0a95d (константа)
                // [1][3][15][2] = {объект описания типов данных - Pattern} аналогично [1][4] += DataTypeSet

                _converter[1][3][15][2] += ExtensionDataTypeSet;
            }

            _converter[1][3][2] += Name;
            _converter[1][3][3][2] += Alias;
            _converter[1][3][4] += Comment;
            _converter[1][4] += DataTypeSet;
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
        private void Comment(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.Comment = source.Value;
        }
        private void DataTypeSet(in ConfigFileReader source, in CancelEventArgs args)
        {
            // Корневой узел объекта "ОписаниеТипов"

            if (source.Token == TokenType.StartObject)
            {
                _typeParser.Parse(in source, out DataTypeSet type);

                _target.DataTypeSet = type;

                //FIXME: extension has higher priority
                //type.Merge(_target.DataTypeSet);
                //_target.DataTypeSet = type;
            }
        }
        private void Parent(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.Parent = source.GetUuid();
        }
        private void ExtensionDataTypeSet(in ConfigFileReader source, in CancelEventArgs args)
        {
            // Корневой узел объекта "ОписаниеТипов"

            if (source.Token != TokenType.StartObject)
            {
                return;
            }

            _typeParser.Parse(in source, out DataTypeSet type);

            _target.ExtensionDataTypeSet = type;

            //FIXME: extension has higher priority
            //_target.DataTypeSet.Merge(in type);
        }
    }
}