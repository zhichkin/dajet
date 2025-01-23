using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class NamedDataTypeDescriptorParser : IMetadataObjectParser
    {
        private readonly OneDbMetadataProvider _cache;
        private ConfigFileParser _parser;
        private DataTypeDescriptorParser _typeParser;

        private MetadataInfo _entry;
        private NamedDataTypeDescriptor _target;
        private ConfigFileConverter _converter;
        public NamedDataTypeDescriptorParser() { }
        public NamedDataTypeDescriptorParser(OneDbMetadataProvider cache) { _cache = cache; }
        public void Parse(in ConfigFileOptions options, out MetadataInfo info)
        {
            _entry = new MetadataInfo()
            {
                MetadataUuid = options.MetadataUuid,
                MetadataType = MetadataTypes.NamedDataTypeDescriptor
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.3.1.2 - uuid объекта метаданных (FileName)

            _converter[1][1] += Reference; // Идентификатор ссылочного типа данных "Ссылка"
            _converter[1][3][2] += Name; // Имя объекта конфигурации

            if (options.IsExtension)
            {
                _converter[1][3][11] += Parent;
            }

            _converter[1][3] += Cancel; // Прервать чтение файла

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
                MetadataType = MetadataTypes.NamedDataTypeDescriptor
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.3.1.2 - uuid объекта метаданных (FileName)

            _converter[1][1] += Reference; // Идентификатор ссылочного типа данных "Ссылка"
            _converter[1][3][2] += Name; // Имя объекта конфигурации
            _converter[1][3] += Cancel; // Прервать чтение файла

            _parser.Parse(in source, in _converter);

            target = _entry;

            _entry = null;
            _parser = null;
            _converter = null;
        }
        public void Parse(in ConfigFileReader source, Guid uuid, out MetadataObject target)
        {
            _target = new NamedDataTypeDescriptor() { Uuid = uuid };
            
            ConfigureConverter();

            _typeParser = new DataTypeDescriptorParser(_cache);

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

            if (_cache != null && _cache.Extension != null) // 1.3.8 = 0 если заимствование отстутствует
            {
                _converter[1][3][11] += Parent; // uuid расширяемого объекта метаданных

                //FIXME: extensions support (!)
                // [1][3][15] - Объект описания дополнительных типов данных определяемого типа
                // [1][3][15][0] = #
                // [1][3][15][1] = f5c65050-3bbb-11d5-b988-0050bae0a95d (константа)
                // [1][3][15][2] = {объект описания типов данных - Pattern} аналогично [1][4] += DataTypeDescriptor

                _converter[1][3][15][2] += ExtensionDataTypeDescriptor;
            }

            _converter[1][3][2] += Name;
            _converter[1][3][3][2] += Alias;
            _converter[1][3][4] += Comment;
            _converter[1][4] += DataTypeDescriptor;
        }
        private void Cancel(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (source.Token == TokenType.EndObject)
            {
                args.Cancel = true;
            }
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
        private void Comment(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.Comment = source.Value;
        }
        private void DataTypeDescriptor(in ConfigFileReader source, in CancelEventArgs args)
        {
            // Корневой узел объекта "ОписаниеТипов"

            if (source.Token == TokenType.StartObject)
            {
                _typeParser.Parse(in source, out DataTypeDescriptor type, out List<Guid> references);

                _target.DataTypeDescriptor = type;

                if (_cache is not null && _cache.ResolveReferences && type.CanBeReference)
                {
                    _target.References.AddRange(references);
                }

                //FIXME: extension has higher priority
                //type.Merge(_target.DataTypeDescriptor);
                //_target.DataTypeDescriptor = type;
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
        private void ExtensionDataTypeDescriptor(in ConfigFileReader source, in CancelEventArgs args)
        {
            // Корневой узел объекта "ОписаниеТипов"

            if (source.Token != TokenType.StartObject)
            {
                return;
            }

            _typeParser.Parse(in source, out DataTypeDescriptor type, out List<Guid> references);

            _target.ExtensionDataTypeDescriptor = type;

            //FIXME: extension has higher priority
            //_target.DataTypeDescriptor.Merge(in type);
        }
    }
}