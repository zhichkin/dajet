using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class ConstantParser : IMetadataObjectParser
    {
        private readonly OneDbMetadataProvider _cache;
        private Constant _target;
        private MetadataInfo _entry;
        private ConfigFileParser _parser;
        private ConfigFileConverter _converter;
        private DataTypeDescriptorParser _typeParser;
        public ConstantParser() { }
        public ConstantParser(OneDbMetadataProvider cache) { _cache = cache; }
        public void Parse(in ConfigFileOptions options, out MetadataInfo info)
        {
            _entry = new MetadataInfo()
            {
                MetadataUuid = options.MetadataUuid,
                MetadataType = MetadataTypes.Constant
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.1.1.1.1.2 - uuid объекта метаданных (FileName)
            
            _converter[1][1][1][1][2] += Name; // Имя объекта метаданных конфигурации
            
            if (options.IsExtension)
            {
                _converter[1][1][1][1][11] += Parent; // uuid расширяемого объекта метаданных
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
                MetadataType = MetadataTypes.Constant
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.1.1.1.1.2 - uuid объекта метаданных (FileName)

            _converter[1][1][1][1][2] += Name; // Имя объекта метаданных конфигурации

            _parser.Parse(in source, in _converter);

            target = _entry;

            _entry = null;
            _parser = null;
            _converter = null;
        }
        public void Parse(in ConfigFileReader reader, Guid uuid, out MetadataObject target)
        {
            _target = new Constant() { Uuid = uuid };

            ConfigureConverter();

            _typeParser = new DataTypeDescriptorParser(_cache);

            _parser = new ConfigFileParser();
            _parser.Parse(in reader, in _converter);

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

            // 1.1.1.1.8 (= 0 если заимствование отстутствует)
            if (_cache is not null && _cache.Extension is not null)
            {
                _converter[1][1][1][1][11] += Parent; // uuid расширяемого объекта метаданных
            }

            _converter[1][1][1][1][2] += Name;
            _converter[1][1][1][1][3][2] += Alias;
            _converter[1][1][1][1][4] += Comment;
            _converter[1][1][1][2] += DataTypeDescriptor;
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