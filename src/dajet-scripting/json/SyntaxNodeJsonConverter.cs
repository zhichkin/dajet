using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaJet.Json
{
    public sealed class SyntaxNodeJsonConverter : JsonConverter<SyntaxNode>
    {
        public override SyntaxNode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
        public override void Write(Utf8JsonWriter writer, SyntaxNode node, JsonSerializerOptions options)
        {
            WriteSyntaxNode(in node, in writer);
        }
        private void WriteSyntaxNode(in SyntaxNode node, in Utf8JsonWriter writer)
        {
            Type type = node.GetType();

            writer.WriteStartObject();

            writer.WriteString("$type", type.FullName);

            foreach (PropertyInfo property in type.GetProperties())
            {
                if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
                {
                    continue;
                }

                object value = property.GetValue(node);

                if (value is null)
                {
                    writer.WriteNull(property.Name);
                }
                else if (value is bool boolean)
                {
                    writer.WriteBoolean(property.Name, boolean);
                }
                else if (value is int number)
                {
                    writer.WriteNumber(property.Name, number);
                }
                else if (value is DateTime datetime)
                {
                    writer.WriteString(property.Name, datetime.ToString("yyyy-MM-ddThh:mm:ss"));
                }
                else if (value is string text)
                {
                    writer.WriteString(property.Name, text);
                }
                else if (value.GetType().IsEnum)
                {
                    writer.WriteString(property.Name, Enum.GetName(value.GetType(), value));
                }
                else if (value is SyntaxNode item)
                {
                    writer.WritePropertyName(property.Name);

                    WriteSyntaxNode(in item, in writer);
                }
                else if (value.GetType().IsListOfSyntaxNodes())
                {
                    if (value is IList list)
                    {
                        writer.WritePropertyName(property.Name);

                        writer.WriteStartArray();

                        for (int i = 0; i < list.Count; i++)
                        {
                            item = list[i] as SyntaxNode;

                            WriteSyntaxNode(in item, in writer);
                        }

                        writer.WriteEndArray();
                    }
                }
                else
                {
                    writer.WriteString(property.Name, value.ToString());
                }
            }

            writer.WriteEndObject();
        }
    }
}