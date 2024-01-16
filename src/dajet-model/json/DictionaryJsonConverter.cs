using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaJet.Json
{
    public sealed class DictionaryJsonConverter : JsonConverter<Dictionary<string,object>>
    {
        public override void Write(Utf8JsonWriter writer, Dictionary<string, object> dictionary, JsonSerializerOptions options)
        {
            string name;
            object value;

            writer.WriteStartObject();

            foreach (var item in dictionary)
            {
                name = item.Key;
                value = item.Value;

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
                else if (value is byte[] binary)
                {
                    writer.WriteString(name, Convert.ToBase64String(binary));
                }
                else if (value is Entity entity)
                {
                    writer.WriteString(name, entity.ToString());
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
                else if (value is List<Dictionary<string, object>> list)
                {
                    writer.WritePropertyName(name);
                    writer.WriteStartArray();
                    foreach (var record in list)
                    {
                        Write(writer, record, options);
                    }
                    writer.WriteEndArray();
                }
                else
                {
                    writer.WriteString(name, value.ToString());
                }
            }

            writer.WriteEndObject();
        }
        public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Dictionary<string, object> parameters = new();

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                ParseObject(ref reader, in parameters);
            }
            else
            {
                throw new JsonException();
            }

            return parameters;
        }
        private void ParseObject(ref Utf8JsonReader reader, in Dictionary<string, object> target)
        {
            string key = null;
            object value = null;
            bool storeValue = false;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    key = reader.GetString();
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
                    if (reader.TryGetDecimal(out decimal number))
                    {
                        if (number.Scale > 0)
                        {
                            value = number;
                        }
                        else
                        {
                            value = Convert.ToInt32(number);
                        }
                    }
                    else
                    {
                        throw new JsonException();
                    }
                    storeValue = true;
                }
                else if (reader.TokenType == JsonTokenType.String)
                {
                    string input = reader.GetString();

                    if (Guid.TryParse(input, out Guid uuid))
                    {
                        value = uuid;
                    }
                    else if (DateTime.TryParse(input, out DateTime dateTime))
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
                            throw new JsonException();
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
                            throw new JsonException();
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
                    List<Dictionary<string, object>> list = new();

                    ParseArray(ref reader, in list);

                    value = list; storeValue = true;
                }
                else if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break; // end of target object - return result
                }
                else
                {
                    throw new JsonException();
                }

                if (storeValue)
                {
                    target.Add(key, value);

                    key = null;
                    value = null;
                    storeValue = false;
                }
            }
        }
        private void ParseArray(ref Utf8JsonReader reader, in List<Dictionary<string, object>> target)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    Dictionary<string, object> item = new();

                    ParseObject(ref reader, in item);

                    target.Add(item);
                }
                else if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break; // end of target object - return result
                }
                else
                {
                    throw new JsonException();
                }
            }
        }
    }
}