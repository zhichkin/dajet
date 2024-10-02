using DaJet.Data;
using DaJet.Json;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System;
using System.Security.Principal;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using TokenType = DaJet.Scripting.TokenType;

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

        private static List<ColumnExpression> CreateMetadataObjectSchema()
        {
            return new List<ColumnExpression>()
            {
                new()
                {
                    Alias = "Ссылка",
                    Expression = new ScalarExpression() { Token = TokenType.Uuid }
                },
                new()
                {
                    Alias = "Код",
                    Expression = new ScalarExpression() { Token = TokenType.Integer }
                },
                new()
                {
                    Alias = "Тип",
                    Expression = new ScalarExpression() { Token = TokenType.String }
                },
                new()
                {
                    Alias = "Имя",
                    Expression = new ScalarExpression() { Token = TokenType.String }
                },
                new()
                {
                    Alias = "ПолноеИмя",
                    Expression = new ScalarExpression() { Token = TokenType.String }
                },
                new()
                {
                    Alias = "Таблица",
                    Expression = new ScalarExpression() { Token = TokenType.String }
                },
                new()
                {
                    Alias = "Владелец",
                    Expression = new ScalarExpression() { Token = TokenType.Uuid }
                }
            };
        }
        [Function("METADATA")] public static DataObject GetMetadataObject(this IScriptRuntime runtime, in string name)
        {
            //TODO: bind data schema to the target object variable : SET @variable = METADATA('Справочник.Номенклатура')

            if (runtime is ScriptScope scope)
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
        [Function("NEWUUID")] public static Guid GenerateNewUuid(this IScriptRuntime runtime)
        {
            return Guid.NewGuid();
        }
        [Function("ERROR_MESSAGE")] public static string GetLastErrorMessage(this IScriptRuntime runtime)
        {
            if (runtime is ScriptScope scope
                && scope.Parent is not null && scope.Parent.Owner is StatementBlock
                && scope.Parent.Parent is not null && scope.Parent.Parent.Owner is TryStatement)
            {
                return scope.Parent.Parent.ErrorMessage; // useful in the CATCH block only
            }

            return string.Empty;
        }

        [Function("TYPEOF")] public static int GetEntityTypeCode(this IScriptRuntime runtime, in Entity entity)
        {
            return entity.TypeCode;
        }
        [Function("TYPEOF")] public static int GetEntityTypeCode(this IScriptRuntime runtime, in string name)
        {
            if (runtime is ScriptScope scope)
            {
                if (!scope.TryGetMetadataProvider(out IMetadataProvider provider, out string error))
                {
                    throw new InvalidOperationException(error);
                }

                MetadataObject metadata = provider.GetMetadataObject(name);

                if (metadata is ApplicationObject entity && entity.TypeCode > 0)
                {
                    return entity.TypeCode; //TODO: check if reference object
                }
            }

            return Entity.Undefined.TypeCode;
        }
        [Function("UUIDOF")] public static Guid GetEntityIdentity(this IScriptRuntime runtime, in Entity entity)
        {
            return entity.Identity;
        }
        [Function("NAMEOF")] public static string GetEntityTypeFullName(this IScriptRuntime runtime, int typeCode)
        {
            if (runtime is ScriptScope scope)
            {
                if (scope.TryGetMetadataProvider(out IMetadataProvider provider, out string error))
                {
                    MetadataItem item = provider.GetMetadataItem(typeCode);

                    MetadataObject metadata = provider.GetMetadataObject(item.Type, item.Uuid);

                    string type = MetadataTypes.ResolveNameRu(item.Type);

                    return $"{type}.{metadata.Name}";
                }
            }

            return string.Empty;
        }
        [Function("NAMEOF")] public static string GetEntityTypeFullName(this IScriptRuntime runtime, in Entity entity)
        {
            if (runtime is ScriptScope scope)
            {
                if (scope.TryGetMetadataProvider(out IMetadataProvider provider, out string error))
                {
                    MetadataItem item = provider.GetMetadataItem(entity.TypeCode);

                    MetadataObject metadata = provider.GetMetadataObject(item.Type, item.Uuid);

                    string type = MetadataTypes.ResolveNameRu(item.Type);

                    return $"{type}.{metadata.Name}";
                }
            }

            return string.Empty;
        }
        [Function("ENTITY")] public static Entity CreateEntity(this IScriptRuntime runtime, int typeCode, Guid identity)
        {
            return new Entity(typeCode, identity);
        }
        [Function("ENTITY")] public static Entity CreateEntity(this IScriptRuntime runtime, in string name, Guid identity)
        {
            if (runtime is ScriptScope scope)
            {
                if (!scope.TryGetMetadataProvider(out IMetadataProvider provider, out string error))
                {
                    throw new InvalidOperationException(error);
                }

                MetadataObject metadata = provider.GetMetadataObject(name);

                if (metadata is ApplicationObject entity && entity.TypeCode > 0)
                {
                    return new Entity(entity.TypeCode, identity); //TODO: check if reference object
                }
            }

            return Entity.Undefined;
        }
    }
}