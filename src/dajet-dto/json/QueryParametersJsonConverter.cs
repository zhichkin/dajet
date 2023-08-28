using DaJet.Model;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaJet.Json
{
    public sealed class QueryParametersJsonConverter : JsonConverter<Dictionary<string, object>>
    {
        public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
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
                throw new FormatException();
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
                        throw new FormatException();
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
                        throw new FormatException(); //TODO: parse EntityObject parameter

                        //if (Entity.TryParse(input, out Entity entity))
                        //{
                        //    value = entity;
                        //}
                        //else
                        //{
                        //    throw new FormatException();
                        //}
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
                    throw new FormatException();
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
            }
        }
        private void ParseEntity(ref Utf8JsonReader reader, in EntityObject target)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    Dictionary<string, object> item = new();

                    ParseObject(ref reader, in item);
                }
                else if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break; // end of target object - return result
                }
            }
        }
    }
}