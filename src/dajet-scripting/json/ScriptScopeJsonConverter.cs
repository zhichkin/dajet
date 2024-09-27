using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaJet.Json
{
    public sealed class ScriptScopeJsonConverter : JsonConverter<BindingScope>
    {
        public override BindingScope Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
        public override void Write(Utf8JsonWriter writer, BindingScope scope, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(nameof(BindingScope.Owner), scope.Owner.GetType().FullName);

            BindingScope ancestor = scope.CloseScope();

            if (ancestor is null)
            {
                writer.WriteNull(nameof(BindingScope.Ancestor));
            }
            else
            {
                writer.WriteString(nameof(BindingScope.Ancestor), ancestor.Owner.GetType().FullName);
            }

            //writer.WritePropertyName("Identifiers");
            //writer.WriteStartArray();
            //foreach (SyntaxNode node in scope.Identifiers)
            //{
            //    writer.WriteStringValue(node.ToString());
            //}
            //writer.WriteEndArray();

            writer.WritePropertyName("Variables");
            writer.WriteStartArray();
            foreach (var item in scope.Variables)
            {
                writer.WriteStringValue($"[{item.Key}] = {item.Value}");
            }
            writer.WriteEndArray();

            writer.WritePropertyName("Tables");
            writer.WriteStartArray();
            foreach (var item in scope.Tables)
            {
                if (item.Value is CommonTableExpression cte)
                {
                    writer.WriteStringValue($"[{cte.Name}] = {item.Value}");
                }
                else
                {
                    writer.WriteStringValue($"[{item.Key}] = {item.Value}");
                }
            }
            writer.WriteEndArray();

            writer.WritePropertyName("Aliases");
            writer.WriteStartArray();
            foreach (var item in scope.Aliases)
            {
                writer.WriteStringValue($"[{item.Key}] = {item.Value}");
            }
            writer.WriteEndArray();

            writer.WritePropertyName("Columns");
            writer.WriteStartArray();
            foreach (var item in scope.Columns)
            {
                writer.WriteStringValue($"[{item.Key}] = {item.Value}");
            }
            writer.WriteEndArray();

            writer.WritePropertyName("Children");
            writer.WriteStartArray();
            foreach (BindingScope node in scope.Children)
            {
                Write(writer, node, options);
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
    }
}