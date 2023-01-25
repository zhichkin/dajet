using DaJet.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaJet.Json
{
    public sealed class ScriptParametersJsonConverter : JsonConverter<Dictionary<string,object>>
    {
        public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string key = null;
            object value = null;
            string input = null;
            bool storeValue = false;
            Dictionary<string, object> parameters = null;

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                parameters = new();
            }
            else
            {
                throw new FormatException();
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    // converter starts parsing from '{'
                }
                else if (reader.TokenType == JsonTokenType.PropertyName)
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
                    if (reader.TryGetDecimal(out decimal numeric))
                    {
                        if (numeric.Scale > 0)
                        {
                            value = numeric;
                        }
                        else
                        {
                            value = Convert.ToInt32(numeric);
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
                    input = reader.GetString();

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
                else if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break; // end of dictionary
                }
                else
                {
                    throw new FormatException();
                }

                if (storeValue)
                {
                    parameters.Add(key, value);

                    key = null;
                    value = null;
                    storeValue = false;
                }
            }

            return parameters;
        }
        public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
