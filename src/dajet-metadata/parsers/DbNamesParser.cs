using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class DbNamesParser
    {
        int _count = 0;
        private DbNameCache _target;
        private ConfigFileParser _parser;
        private ConfigFileConverter _converter;
        public void Parse(in ConfigFileReader reader, out DbNameCache cache)
        {
            ConfigureConverter();

            _target = new DbNameCache();
            _parser = new ConfigFileParser();
            _parser.Parse(in reader, in _converter);

            cache = _target;

            _count = 0;
            _target = null;
            _parser = null;
            _converter = null;
        }
        private void ConfigureConverter()
        {
            _converter = new ConfigFileConverter();

            _converter[1][0] += Count; // Количество элементов файла DbNames
        }
        private void Count(in ConfigFileReader source, in CancelEventArgs args)
        {
            _count = source.GetInt32();

            int code;
            Guid uuid;
            string name;

            while (_count > 0)
            {
                if (!source.Read()) { break; }

                if (source.Token == TokenType.StartObject)
                {
                    uuid = Uuid(in source); // 1.x.0 - уникальный идентификатор объекта метаданных
                    name = Name(in source); // 1.x.1 - имя объекта СУБД (как правило префикс)
                    code = Code(in source); // 1.x.2 - уникальный целочисленный код объекта метаданных

                    if (code > 0 && uuid != Guid.Empty && name != null)
                    {
                        _target.Add(uuid, code, name);
                    }
                }
                else if (source.Token == TokenType.EndObject)
                {
                    _count--;
                }
            }
        }
        private Guid Uuid(in ConfigFileReader source)
        {
            if (source.Read()) 
            {
                return source.GetUuid();
            }
            return Guid.Empty;
        }
        private string Name(in ConfigFileReader source)
        {
            if (source.Read())
            {
                return source.Value;
            }
            return null;
        }
        private int Code(in ConfigFileReader source)
        {
            if (source.Read())
            {
                return source.GetInt32();
            }
            return -1;
        }
    }
}