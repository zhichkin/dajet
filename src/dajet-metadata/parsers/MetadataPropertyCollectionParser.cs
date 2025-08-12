using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class MetadataPropertyCollectionParser
    {
        private readonly OneDbMetadataProvider _cache;
        private ConfigFileParser _parser;
        private DataTypeDescriptorParser _typeParser;
        private readonly MetadataObject _owner;

        private int _count; // количество свойств
        private PropertyPurpose _purpose;
        private MetadataProperty _property;
        private List<MetadataProperty> _target;
        private ConfigFileConverter _converter;
        ///<summary>
        ///FIXME: если cache равен null, то обработка идентификаторов ссылочных типов не выполняется.
        ///<br>Дополнительная информация: <see cref="DataTypeDescriptorParser"/></br>
        ///</summary>
        public MetadataPropertyCollectionParser(OneDbMetadataProvider cache)
        {
            _cache = cache;
        }
        ///<summary>
        ///FIXME: если cache равен null, то обработка идентификаторов ссылочных типов не выполняется.
        ///<br>Дополнительная информация: <see cref="DataTypeDescriptorParser"/></br>
        ///</summary>
        ///<param name="owner">Используется для определения вида свойства: реквизит, ресурс или измерение.</param>
        public MetadataPropertyCollectionParser(OneDbMetadataProvider cache, MetadataObject owner)
        {
            _cache = cache;
            _owner = owner;
        }
        public void Parse(in ConfigFileReader source, out List<MetadataProperty> target)
        {
            ConfigureCollectionConverter(in source);

            _target = new List<MetadataProperty>();

            _typeParser = new DataTypeDescriptorParser(_cache);

            _parser = new ConfigFileParser();
            _parser.Parse(in source, in _converter);

            // result
            target = _target;

            // dispose private variables
            _target = null;
            _parser = null;
            _property = null;
            _converter = null;
            _typeParser = null;
        }
        
        private void ConfigureCollectionConverter(in ConfigFileReader source)
        {
            _converter = new ConfigFileConverter();

            // Параметр source должен быть позиционирован в данный момент
            // на узле коллекции свойств объекта метаданных (токен = '{')
            // source.Char == '{' && source.Token == TokenType.StartObject
            _converter = _converter.Path(source.Level - 1, source.Path);

            // Необходимо прекратить чтение коллекции,
            // чтобы позволить другим парсерам выполнить свою работу
            // по чтению потока байт source (данный парсер является вложенным)
            _converter += Cancel;

            // Свойства типизированной коллекции
            _converter[0] += Uuid; // идентификатор типа коллекции
            _converter[1] += Count; // количество элементов в коллекции

            // Объекты элементов коллекции, в зависимости от значения _converter[1],
            // располагаются в коллекции последовательно по адресам _converter[2..N]
        }
        private void Cancel(in ConfigFileReader source, in CancelEventArgs args)
        {
            if (source.Token == TokenType.EndObject)
            {
                args.Cancel = true;
            }
        }
        private void Uuid(in ConfigFileReader source, in CancelEventArgs args)
        {
            // тип коллекции свойств
            Guid type = source.GetUuid();

            if (type == SystemUuid.InformationRegister_Measure ||
                type == SystemUuid.AccumulationRegister_Measure ||
                type == SystemUuid.AccountingRegister_Measure)
            {
                _purpose = PropertyPurpose.Measure;
            }
            else if (type == SystemUuid.InformationRegister_Dimension
                || type == SystemUuid.AccumulationRegister_Dimension
                || type == SystemUuid.AccountingRegister_Dimension)
            {
                _purpose = PropertyPurpose.Dimension;
            }
            else if (type == SystemUuid.Account_AccountingFlags)
            {
                _purpose = PropertyPurpose.AccountingFlag;
            }
            else if (type == SystemUuid.Account_AccountingDimensionFlags)
            {
                _purpose = PropertyPurpose.AccountingDimensionFlag;
            }
            else if (type == SystemUuid.BusinessTask_Routing_Property)
            {
                _purpose = PropertyPurpose.RoutingProperty;
            }
            else
            {
                _purpose = PropertyPurpose.Property;
            }
        }
        private void Count(in ConfigFileReader source, in CancelEventArgs args)
        {
            _count = source.GetInt32();
            
            ConfigureItemConverters();
        }
        
        private void ConfigureItemConverters()
        {
            int offset = 2; // начальный индекс для узлов элементов коллекции от её корня

            for (int n = 0; n < _count; n++)
            {
                ConfigureItemConverter(offset + n);
            }
        }
        private void ConfigureItemConverter(int offset)
        {
            _converter[offset] += MetadataPropertyConverter;
        }
        private void MetadataPropertyConverter(in ConfigFileReader source, in CancelEventArgs args)
        {
            // начало чтения объекта свойства
            if (source.Token == TokenType.StartObject)
            {
                // корневой узел объекта свойства
                _converter = _converter.Path(source.Level - 1, source.Path);

                _property = new MetadataProperty() { Purpose = _purpose };

                // Свойство объекта:
                // -----------------
                // [6][2] 0.1.1.1.8 = 0 если заимствование отстутствует
                // [6][2] 0.1.1.1.11 - uuid расширяемого объекта метаданных
                // [6][2] 0.1.1.1.15 - Объект описания дополнительных типов данных свойства
                // [6][2] 0.1.1.1.15.0 = #
                // [6][2] 0.1.1.1.15.1 = f5c65050-3bbb-11d5-b988-0050bae0a95d (константа)
                // [6][2] 0.1.1.1.15.2 = {объект описания типов данных - Pattern} [0][1][1][2] += PropertyType

                // Свойство табличной части:
                // -------------------------
                // [5][2] 0.1.1.1.8 = 0 если заимствование отстутствует
                // [5][2] 0.1.1.1.11 - uuid расширяемого объекта метаданных
                // [5][2] 0.1.1.1.15 - Объект описания дополнительных типов данных свойства
                // [5][2] 0.1.1.1.15.0 = #
                // [5][2] 0.1.1.1.15.1 = f5c65050-3bbb-11d5-b988-0050bae0a95d (константа)
                // [5][2] 0.1.1.1.15.2 = {объект описания типов данных - Pattern} аналогично [0][1][1][2] += PropertyType

                if (_cache != null && _cache.Extension != null) // 0.1.1.1.8 = 0 если заимствование отстутствует
                {
                    _converter[0][1][1][1][11] += Parent; // uuid расширяемого объекта метаданных
                    _converter[0][1][1][1][15][2] += ExtensionPropertyType;
                }

                _converter[0][1][1][1][1][2] += PropertyUuid;
                _converter[0][1][1][1][2] += PropertyName;
                _converter[0][1][1][1][3][2] += PropertyAlias;
                _converter[0][1][1][2] += PropertyType;

                if (_owner is not null)
                {
                    if (_owner is Catalog || _owner is Characteristic)
                    {
                        _converter[0][3] += PropertyUsage;
                    }
                    else if (_owner is InformationRegister)
                    {
                        if (_purpose == PropertyPurpose.Dimension)
                        {
                            _converter[0][2] += CascadeDelete;
                            _converter[0][5] += UseDimensionForChangeTracking;
                        }
                    }
                    else if (_owner is AccountingRegister)
                    {
                        if (_purpose == PropertyPurpose.Dimension)
                        {
                            _converter[0][2] += IsBalance; // Балансовый
                            _converter[0][3] += AccountingFlag; // Признак учёта
                        }
                        else if (_purpose == PropertyPurpose.Measure)
                        {
                            _converter[0][2] += IsBalance; // Балансовый
                            _converter[0][3] += AccountingFlag; // Признак учёта
                            _converter[0][4] += AccountingDimensionFlag; // Признак учёта субконто
                        }
                    }
                    else if (_owner is BusinessTask)
                    {
                        if (_purpose == PropertyPurpose.RoutingProperty)
                        {
                            _converter[0][3] += RoutingDimension; // Измерение адресации

                        }
                    }
                }
            }

            // завершение чтения объекта свойства
            if (source.Token == TokenType.EndObject)
            {
                _target.Add(_property);
            }
        }
        private void PropertyUuid(in ConfigFileReader source, in CancelEventArgs args)
        {
            _property.Uuid = source.GetUuid();
        }
        private void PropertyName(in ConfigFileReader source, in CancelEventArgs args)
        {
            _property.Name = source.Value;
        }
        private void PropertyAlias(in ConfigFileReader source, in CancelEventArgs args)
        {
            _property.Alias = source.Value;
        }
        private void PropertyType(in ConfigFileReader source, in CancelEventArgs args)
        {
            // Корневой узел объекта "ОписаниеТипов"

            if (source.Token == TokenType.StartObject)
            {
                _typeParser.Parse(in source, out DataTypeDescriptor type, out List<Guid> references);

                _property.PropertyType = type;

                if (_cache is not null && _cache.ResolveReferences && type.CanBeReference)
                {
                    _property.References.AddRange(references);
                }
            }
        }
        private void PropertyUsage(in ConfigFileReader source, in CancelEventArgs args)
        {
            _property.PropertyUsage = (PropertyUsage)source.GetInt32();
        }
        private void CascadeDelete(in ConfigFileReader source, in CancelEventArgs args)
        {
            _property.CascadeDelete = (source.GetInt32() == 1);
        }
        private void UseDimensionForChangeTracking(in ConfigFileReader source, in CancelEventArgs args)
        {
            _property.UseForChangeTracking = (source.GetInt32() == 1);
        }
        private void Parent(in ConfigFileReader source, in CancelEventArgs args)
        {
            _property.Parent = source.GetUuid();
        }
        private void ExtensionPropertyType(in ConfigFileReader source, in CancelEventArgs args)
        {
            // Корневой узел объекта "ОписаниеТипов"

            if (source.Token != TokenType.StartObject)
            {
                return;
            }

            _typeParser.Parse(in source, out DataTypeDescriptor type, out List<Guid> references);

            _property.ExtensionPropertyType = type;

            //FIXME: extension has higher priority
            //_target.DataTypeDescriptor.Merge(in type);
        }
        private void IsBalance(in ConfigFileReader source, in CancelEventArgs args)
        {
            _property.IsBalance = (source.GetInt32() == 1);
        }
        private void AccountingFlag(in ConfigFileReader source, in CancelEventArgs args)
        {
            _property.AccountingFlag = source.GetUuid();
        }
        private void AccountingDimensionFlag(in ConfigFileReader source, in CancelEventArgs args)
        {
            _property.AccountingDimensionFlag = source.GetUuid();
        }
        private void RoutingDimension(in ConfigFileReader source, in CancelEventArgs args)
        {
            _property.RoutingDimension = source.GetUuid();
        }
    }
}