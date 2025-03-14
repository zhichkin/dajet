﻿using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class AccountingRegisterParser : IMetadataObjectParser
    {
        private readonly OneDbMetadataProvider _cache;
        private ConfigFileParser _parser;
        private MetadataPropertyCollectionParser _propertyParser;

        private MetadataInfo _entry;
        private AccountingRegister _target;
        private ConfigFileConverter _converter;
        public AccountingRegisterParser() { }
        public AccountingRegisterParser(OneDbMetadataProvider cache) { _cache = cache; }
        public void Parse(in ConfigFileOptions options, out MetadataInfo info)
        {
            _entry = new MetadataInfo()
            {
                MetadataUuid = options.MetadataUuid,
                MetadataType = MetadataTypes.AccountingRegister
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.15.1.1.2 - uuid объекта метаданных (FileName)

            _converter[1][15][1][2] += Name; // Имя объекта конфигурации

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
                MetadataType = MetadataTypes.AccountingRegister
            };

            _parser = new ConfigFileParser();
            _converter = new ConfigFileConverter();

            // 1.15.1.1.2 - uuid объекта метаданных (FileName)

            _converter[1][15][1][2] += Name; // Имя объекта конфигурации
            _converter[1][15][1] += Cancel;

            _parser.Parse(in source, in _converter);

            target = _entry;

            _entry = null;
            _parser = null;
            _converter = null;
        }
        public void Parse(in ConfigFileReader reader, Guid uuid, out MetadataObject target)
        {
            _target = new AccountingRegister() { Uuid = uuid };

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

            _converter[1][15][1][2] += Name; // Имя
            _converter[1][15][1][3][2] += Alias; // Синоним
            _converter[1][18] += ChartOfAccounts; // План счетов
            _converter[1][20] += UseCorrespondence; // Корреспонденция
            _converter[1][23] += UseSplitter; // Разрешить разделение итогов

            // коллекции свойств регистра бухгалтерии
            _converter[3] += PropertyCollection; // измерения 35b63b9d-0adf-4625-a047-10ae874c19a3
            _converter[5] += PropertyCollection; // ресурсы   63405499-7491-4ce3-ac72-43433cbe4112
            _converter[7] += PropertyCollection; // реквизиты 9d28ee33-9c7e-4a1b-8f13-50aa9b36607b
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
        private void ChartOfAccounts(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.ChartOfAccounts = source.GetUuid();
        }
        private void UseCorrespondence(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.UseCorrespondence = (source.GetInt32() == 1);
        }
        private void UseSplitter(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target.UseSplitter = (source.GetInt32() != 0);
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