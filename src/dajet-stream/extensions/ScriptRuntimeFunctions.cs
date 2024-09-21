using DaJet.Data;
using DaJet.Json;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using DaJet.Stream;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Runtime
{
    public static class ScriptRuntimeFunctions
    {
        private static readonly JsonWriterOptions JsonWriterOptions = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private static readonly DataObjectJsonConverter JsonConverter = new();
        static ScriptRuntimeFunctions()
        {
            SerializerOptions.Converters.Add(new DataObjectJsonConverter());
        }
        [Function("JSON")] public static object FromJson(this IScriptRuntime runtime, in string json)
        {
            Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(json), true, default);

            if (json[0] == '{')
            {
                return JsonConverter.Read(ref reader, typeof(DataObject), SerializerOptions);
            }
            else if (json[0] == '[')
            {
                return JsonSerializer.Deserialize(json, typeof(List<DataObject>), SerializerOptions);
            }
            else
            {
                throw new FormatException(nameof(json));
            }
        }
        [Function("JSON")] public static string ToJson(this IScriptRuntime runtime, in DataObject record)
        {
            using (MemoryStream memory = new())
            {
                using (Utf8JsonWriter writer = new(memory, JsonWriterOptions))
                {
                    JsonConverter.Write(writer, record, null);

                    writer.Flush();

                    string json = Encoding.UTF8.GetString(memory.ToArray());

                    return json;
                }
            }
        }
        [Function("JSON")] public static string ToJson(this IScriptRuntime runtime, in List<DataObject> table)
        {
            return JsonSerializer.Serialize(table, SerializerOptions);
        }

        [Function("METADATA")] public static DataObject GetMetadataObject(this IScriptRuntime runtime, in string name)
        {
            if (runtime is StreamScope scope)
            {
                if (!scope.TryGetMetadataProvider(out IMetadataProvider provider, out string error))
                {
                    throw new InvalidOperationException(error);
                }

                MetadataObject metadata = provider.GetMetadataObject(name);

                if (metadata is not null)
                {
                    return metadata.ToDataObject();
                }
            }

            return null;
        }
        [Function("NOW")] public static DateTime GetCurrentDateTime(this IScriptRuntime runtime)
        {
            return DateTime.Now;
        }
        [Function("ERROR_MESSAGE")] public static string GetLastErrorMessage(this IScriptRuntime runtime)
        {
            if (runtime is StreamScope scope
                && scope.Parent is not null && scope.Parent.Owner is StatementBlock
                && scope.Parent.Parent is not null && scope.Parent.Parent.Owner is TryStatement)
            {
                return scope.Parent.Parent.ErrorMessage; // this is only useful in the CATCH block
            }

            return string.Empty;
        }
    }
}