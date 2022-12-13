using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class CatalogParser : IMetadataObjectParser
    {
        private readonly MetadataCache _cache;
        private ConfigFileParser _parser;
        private TablePartCollectionParser _tableParser;
        private MetadataPropertyCollectionParser _propertyParser;

        private Catalog _target;
        private MetadataInfo _entry;
        private ConfigFileConverter _converter;
        public CatalogParser() { }
        public CatalogParser(MetadataCache cache) { _cache = cache; }
        public void Parse(in ConfigFileOptions options, out MetadataInfo info)
        {
            _entry = new MetadataInfo()
            {
                MetadataUuid = options.MetadataUuid,
                MetadataType = MetadataTypes.Catalog
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.9.1.1.2 - uuid объекта метаданных (FileName)

            _converter[1][3] += Reference; // Идентификатор ссылочного типа данных, например, "СправочникСсылка.Номенклатура"
            _converter[1][9][1][2] += Name; // Имя объекта метаданных конфигурации
            _converter[1][12] += Owners; // Коллекция владельцев справочника

            if (options.IsExtension)
            {
                _converter[1][9][1][9] += Parent;
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
                MetadataType = MetadataTypes.Catalog
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.9.1.1.2 - uuid объекта метаданных (FileName)

            _converter[1][3] += Reference; // Идентификатор ссылочного типа данных, например, "СправочникСсылка.Номенклатура"
            _converter[1][9][1][2] += Name; // Имя объекта метаданных конфигурации
            _converter[1][12] += Owners; // Коллекция владельцев справочника

            _parser.Parse(in source, in _converter);

            target = _entry;

            _entry = null;
            _parser = null;
            _converter = null;
        }
        public void Parse(in ConfigFileReader reader, Guid uuid, out MetadataObject target)
        {
            _target = new Catalog() { Uuid = uuid };

            ConfigureConverter();

            _parser = new ConfigFileParser();
            _tableParser = new TablePartCollectionParser(_cache);
            _propertyParser = new MetadataPropertyCollectionParser(_cache, _target);

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

            // 1.9.1.6 (= 0 если заимствование отстутствует) смещение ??? "1"х2+1 = [1][9][1][9]
            if (_cache.Extension != null) // 1.9.1.8 = 0 если заимствование отстутствует
            {
                _converter[1][9][1][9] += Parent; // uuid расширяемого объекта метаданных
            }

            _converter[1][9][1][2] += Name;
            _converter[1][9][1][3][2] += Alias;
            _converter[1][17] += CodeLength;
            _converter[1][18] += CodeType;
            _converter[1][19] += DescriptionLength;
            _converter[1][36] += HierarchyType;
            _converter[1][37] += IsHierarchical;

            _converter[5] += TablePartCollection; // 932159f9-95b2-4e76-a8dd-8849fe5c5ded - идентификатор коллекции табличных частей
            _converter[6] += PropertyCollection; // cf4abea7-37b2-11d4-940f-008048da11f9 - идентификатор коллекции реквизитов
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
        private void Owners(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (source.Token == TokenType.EndObject)
            {
                args.Cancel = true;
                
                return;
            }

            // 1.12.0 - UUID коллекции владельцев справочника !?
            // 1.12.1 - количество владельцев справочника
            // 1.12.N - описание владельцев
            // 1.12.N.2.1 - uuid'ы владельцев (file names)

            _ = source.Read(); // [1][12][0] - UUID коллекции владельцев справочника
            _ = source.Read(); // [1][12][1] - количество владельцев справочника

            int count = source.GetInt32();

            if (count == 0)
            {
                return;
            }

            int offset = 2; // начальный индекс N [1][12][2]

            for (int n = 0; n < count; n++)
            {
                _converter[1][12][offset + n][2][1] += OwnerUuid;
            }
        }
        private void OwnerUuid(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (_entry != null)
            {
                _entry.CatalogOwners.Add(source.GetUuid());
            }
        }
        private void CodeType(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.CodeType = (CodeType)source.GetInt32();
        }
        private void CodeLength(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.CodeLength = source.GetInt32();
        }
        private void DescriptionLength(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.DescriptionLength = source.GetInt32();
        }
        private void HierarchyType(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.HierarchyType = (HierarchyType)source.GetInt32();
        }
        private void IsHierarchical(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.IsHierarchical = (source.GetInt32() != 0);
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