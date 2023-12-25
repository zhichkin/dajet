using DaJet.Scripting.Model;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaJet.Scripting
{
    public sealed class ScriptScopeJsonConverter : JsonConverter<ScriptScope>
    {
        public override ScriptScope Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
        public override void Write(Utf8JsonWriter writer, ScriptScope scope, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString("Type", Enum.GetName(scope.Type));
            writer.WriteString("Owner", scope.Owner.GetType().FullName);

            writer.WritePropertyName("Identifiers");
            writer.WriteStartArray();
            foreach (SyntaxNode node in scope.Identifiers)
            {
                writer.WriteStringValue(node.ToString());
            }
            writer.WriteEndArray();

            writer.WritePropertyName("Children");
            writer.WriteStartArray();
            foreach (ScriptScope node in scope.Children)
            {
                Write(writer, node, options);
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
    }
}