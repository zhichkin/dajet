using DaJet.Data;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaJet.Json
{
    public sealed class DataObjectJsonConverter : JsonConverter<DataObject>
    {
        public override DataObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            DataObject record = new();

            string name = null;
            object value = null;
            string input = null;
            bool storeValue = false;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    // converter starts parsing from '{'
                    
                    if (name is not null) // read nested DataObject
                    {
                        value = Read(ref reader, typeof(DataObject), options);
                        storeValue = true;
                    }
                }
                else if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    name = reader.GetString();
                }
                else if (reader.TokenType == JsonTokenType.Null)
                {
                    value = null; storeValue = true;
                }
                else if (reader.TokenType == JsonTokenType.True)
                {
                    value = true; storeValue = true;
                }
                else if (reader.TokenType == JsonTokenType.False)
                {
                    value = false; storeValue = true;
                }
                else if (reader.TokenType == JsonTokenType.Number)
                {
                    if (reader.TryGetDecimal(out decimal numeric))
                    {
                        if (numeric.Scale > 0) { value = numeric; }
                        else { value = decimal.ToInt32(numeric); }
                    }
                    else
                    {
                        throw new FormatException();
                    }
                    storeValue = true;
                }
                else if (reader.TokenType == JsonTokenType.String)
                {
                    input = reader.GetString();

                    if (Guid.TryParse(input, out Guid uuid))
                    {
                        value = uuid;
                    }
                    else if (input.Length >= 10 && DateTime.TryParse(input, out DateTime dateTime))
                    {
                        value = dateTime;
                    }
                    else if (input.StartsWith('{'))
                    {
                        if (Entity.TryParse(input, out Entity entity))
                        {
                            value = entity;
                        }
                        else
                        {
                            throw new FormatException();
                        }
                    }
                    else if (input.StartsWith("0x"))
                    {
                        try
                        {
                            value = Convert.FromHexString(input.AsSpan(2)); // remove leading 0x
                        }
                        catch
                        {
                            throw new FormatException();
                        }
                    }
                    else
                    {
                        value = input;
                    }
                    storeValue = true;
                }
                else if (reader.TokenType == JsonTokenType.StartArray)
                {
                    DataObject item;
                    List<DataObject> list = new();
                    do
                    {
                        item = Read(ref reader, typeof(DataObject), options);

                        if (item is not null) { list.Add(item); }
                    }
                    while (item is not null);

                    value = list; storeValue = true;
                }
                else if (reader.TokenType == JsonTokenType.EndArray)
                {
                    return null; // end of array
                }
                else if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break; // end of object
                }
                else //TODO: process DaJet.Union
                {
                    throw new FormatException();
                }

                if (storeValue)
                {
                    record.SetValue(name, value);

                    name = null;
                    value = null;
                    storeValue = false;
                }
            }

            return record;
        }
        public override void Write(Utf8JsonWriter writer, DataObject record, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            string name;
            object value;

            for (int i = 0; i < record.Count(); i++)
            {
                name = record.GetName(i);
                value = record.GetValue(i);

                if (value is null)
                {
                    writer.WriteNull(name);
                }
                else if (value is string text)
                {
                    writer.WriteString(name, text);
                }
                else if (value is bool boolean)
                {
                    writer.WriteBoolean(name, boolean);
                }
                else if (value is Guid uuid)
                {
                    writer.WriteString(name, uuid);
                }
                else if (value is DateTime dateTime)
                {
                    writer.WriteString(name, dateTime.ToString("yyyy-MM-ddTHH:mm:ss"));
                }
                else if (value is Entity entity)
                {
                    writer.WriteString(name, entity.ToString());
                }
                else if (value is byte[] binary)
                {
                    writer.WriteString(name, Convert.ToBase64String(binary));
                }
                else if (value is float dec4) { writer.WriteNumber(name, dec4); }
                else if (value is double dec8) { writer.WriteNumber(name, dec8); }
                else if (value is decimal dec16) { writer.WriteNumber(name, dec16); }
                else if (value is sbyte int1) { writer.WriteNumber(name, int1); }
                else if (value is short int2) { writer.WriteNumber(name, int2); }
                else if (value is int int4) { writer.WriteNumber(name, int4); }
                else if (value is long int8) { writer.WriteNumber(name, int8); }
                else if (value is byte uint1) { writer.WriteNumber(name, uint1); }
                else if (value is ushort uint2) { writer.WriteNumber(name, uint2); }
                else if (value is uint uint4) { writer.WriteNumber(name, uint4); }
                else if (value is ulong uint8) { writer.WriteNumber(name, uint8); }
                else if (value is DataObject data)
                {
                    writer.WritePropertyName(name);
                    Write(writer, data, options);
                }
                else if (value is List<DataObject> list)
                {
                    writer.WritePropertyName(name);
                    writer.WriteStartArray();
                    foreach (DataObject item in list)
                    {
                        Write(writer, item, options);
                    }
                    writer.WriteEndArray();
                }
                else //TODO: process DaJet.Union
                {
                    writer.WriteString(name, value.ToString());
                }
            }

            writer.WriteEndObject();
        }
    }
}