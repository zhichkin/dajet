using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;

namespace DaJet.Metadata.Parsers
{
    ///<summary>Парсер для чтения объекта "ОписаниеТипов".</summary>
    public sealed class DataTypeDescriptorParser
    {
        private readonly MetadataCache _cache;
        private int _pointer;
        private string[] _qualifiers = new string[3];
        ///<param name="cache">Если cache равен null, то обработка идентификаторов ссылочных типов не выполняется</param>
        public DataTypeDescriptorParser(MetadataCache cache) { _cache = cache; }
        ///<summary>
        ///Объект чтения файла метаданных <see cref="ConfigFileReader"/> перед вызовом этого метода
        ///<br>
        ///должен быть позиционирован на корневом узле объекта описания типа данных:
        ///</br>
        ///<br>
        ///<c><![CDATA[source.Char == '{' && source.Token == TokenType.StartObject]]></c>
        ///</br>
        ///</summary>
        ///<param name="source">Объект чтения файла метаданных.</param>
        ///<param name="target">Объект описания типа данных.</param>
        public void Parse(in ConfigFileReader source, out DataTypeDescriptor target)
        {
            target = new DataTypeDescriptor();
            List<Guid> references = new();

            ParseDataTypeDescriptor(in source, in target, in references);

            if (_cache == null) // Обработка идентификаторов ссылочных типов не требуется
            {
                target.Identifiers = references;
            }
            else if (references.Count > 0)
            {
                // Конфигурирование ссылочных типов данных объекта "ОписаниеТипов".
                // Внимание!
                // Если описание типов ссылается на определяемый тип или характеристику,
                // которые не являются или не содержат в своём составе ссылочные типы данных,
                // то в таком случае описание типов будет содержать только примитивные типы данных.
                Configurator.ConfigureDataTypeDescriptor(in _cache, in target, in references);

                //REFACTORING(29.01.2023)
                //THINK: add setting to MetadataCache to resolve references optionally !?
                //List<MetadataItem> list = _cache.ResolveReferences(in references);
                //target.References.AddRange(list);
            }
        }
        private void ParseDataTypeDescriptor(in ConfigFileReader source, in DataTypeDescriptor target, in List<Guid> references)
        {
            _ = source.Read(); // 0 index
            if (source.Value != "Pattern")
            {
                return; // Это не объект "ОписаниеТипов" !
            }

            while (source.Read())
            {
                if (source.Token == TokenType.EndObject)
                {
                    break;
                }
                else if (source.Token == TokenType.StartObject)
                {
                    // read the next data type description
                    _pointer = -1;
                    _qualifiers[0] = null;
                    _qualifiers[1] = null;
                    _qualifiers[2] = null;
                }
                else if (source.Token == TokenType.Value || source.Token == TokenType.String)
                {
                    if (source.Path[source.Level] == 0) // 0 - Pointer _Fld + _TYPE
                    {
                        if (source.Value == MetadataTokens.B) // {"B"}
                        {
                            ReadBoolean(in source, in target); // _Fld + _L
                        }
                        else if (source.Value == MetadataTokens.N) // {"N",10,2,0} | {"N",10,2,1}
                        {
                            ReadNumeric(in source, in target); // _Fld + _N
                        }
                        else if (source.Value == MetadataTokens.D) // {"D"} | {"D","D"} | {"D","T"}
                        {
                            ReadDateTime(in source, in target); // _Fld + _T
                        }
                        else if (source.Value == MetadataTokens.S) // {"S"} | {"S",10,0} | {"S",10,1}
                        {
                            // NOTE: Строки неограниченной длины не поддерживают составной тип данных.
                            ReadString(in source, in target); // _Fld + _S
                        }
                        else if (source.Value == MetadataTokens.R) // {"#",70497451-981e-43b8-af46-fae8d65d16f2}
                        {
                            ReadReference(in source, in target, in references); // _Fld + _RTRef + _RRRef
                        }
                    }
                }
            }
        }
        private void ReadQualifiers(in ConfigFileReader reader)
        {
            while (reader.Read())
            {
                if (reader.Token == TokenType.EndObject)
                {
                    break;
                }
                else if (reader.Token == TokenType.Value || reader.Token == TokenType.String)
                {
                    if (reader.Value == null) { continue; }

                    _pointer++;
                    _qualifiers[_pointer] = reader.Value;
                }
            }
        }
        private void ReadBoolean(in ConfigFileReader reader, in DataTypeDescriptor target)
        {
            target.CanBeBoolean = true;

            ReadQualifiers(in reader);
        }
        private void ReadDateTime(in ConfigFileReader reader, in DataTypeDescriptor target)
        {
            target.CanBeDateTime = true;

            ReadQualifiers(in reader);

            if (_pointer == -1)
            {
                target.DateTimePart = DateTimePart.DateTime;
            }
            else if (_pointer == 0 && _qualifiers[_pointer] == MetadataTokens.D)
            {
                target.DateTimePart = DateTimePart.Date;
            }
            else
            {
                target.DateTimePart = DateTimePart.Time;
            }
        }
        private void ReadString(in ConfigFileReader reader, in DataTypeDescriptor target)
        {
            target.CanBeString = true;

            ReadQualifiers(in reader);

            if (_pointer == -1)
            {
                // Строка неограниченной длины - nvarchar(max)
                target.StringLength = 0; // Не может быть составным типом !
                target.StringKind = StringKind.Variable;
            }
            else if (_pointer == 1)
            {
                target.StringLength = int.Parse(_qualifiers[0]);
                target.StringKind = (StringKind)int.Parse(_qualifiers[1]);
            }
        }
        private void ReadNumeric(in ConfigFileReader reader, in DataTypeDescriptor target)
        {
            target.CanBeNumeric = true;

            ReadQualifiers(in reader);

            if (_pointer == 2)
            {
                target.NumericPrecision = int.Parse(_qualifiers[0]);
                target.NumericScale = int.Parse(_qualifiers[1]);
                target.NumericKind = (NumericKind)int.Parse(_qualifiers[2]);
            }
        }
        private void ReadReference(in ConfigFileReader reader, in DataTypeDescriptor target, in List<Guid> references)
        {
            ReadQualifiers(in reader);

            if (_pointer != 0) { return; }

            Guid type = new(_qualifiers[_pointer]);

            if (type == SingleTypes.ValueStorage) // ХранилищеЗначения - varbinary(max)
            {
                target.IsValueStorage = true; // Не может быть составным типом !
            }
            else if (type == SingleTypes.UniqueIdentifier) // УникальныйИдентификатор - binary(16)
            {
                target.IsUuid = true; // Не может быть составным типом !
            }
            
            references.Add(type);
        }
    }
}

//{"Pattern",
//{"S",10,1},
//{"N",10,2,0}
//}

//[1](1.4) DaJet.Metadata.Model.ConfigObject - корень объекта "ОписаниеТипов"
//---[2](1.4.0) "Pattern"
//---[2](1.4.1) DaJet.Metadata.Model.ConfigObject - корень объекта описания одного типа данных
//------[3](1.4.1.0) "S"
//------[3](1.4.1.1) "10"
//------[3](1.4.1.2) "1"
//---[2](1.4.2) DaJet.Metadata.Model.ConfigObject - корень объекта описания одного типа данных
//------[3](1.4.2.0) "N"
//------[3](1.4.2.1) "10"
//------[3](1.4.2.2) "2"
//------[3](1.4.2.3) "0"