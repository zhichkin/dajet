using DaJet.Data;
using DaJet.Json;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Scripting;
using DaJet.Scripting.Model;
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

        [Function("ENTITY")] public static Entity CreateEntity(this IScriptRuntime runtime, in string name)
        {
            return CreateEntity(runtime, in name, Guid.Empty);
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
        [Function("TYPEOF")] public static int GetEntityTypeCode(this IScriptRuntime runtime, in Entity entity)
        {
            return entity.TypeCode;
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

        [Function("PROPERTY_COUNT")] public static int PropertyCount(this IScriptRuntime runtime, in DataObject record)
        {
            if (record is null)
            {
                return 0;
            }

            return record.Count();
        }
        [Function("PROPERTY_EXISTS")] public static bool PropertyExists(this IScriptRuntime runtime, in DataObject record, in string name)
        {
            if (record is null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return record.Contains(name);
        }
        [Function("GET_PROPERTY")] public static DataObject GetProperty(this IScriptRuntime runtime, in DataObject record, int index)
        {
            //TODO: bind data schema to the target object variable : SET @variable = GET_PROPERTY(@object, @index)

            if (record is null || index < 0 || index >= record.Count())
            {
                return null;
            }

            string name = record.GetName(index);
            object value = record.GetValue(index);
            string type = (value is null) ? "NULL" : ParserHelper.GetDataTypeLiteral(value.GetType());

            DataObject property = new(3);
            property.SetValue("Name", name);
            property.SetValue("Type", type);
            property.SetValue("Value", value);

            //if (runtime is ScriptScope scope)
            //{
            //    if (!scope.TryGetDeclaration("???", out _, out DeclareStatement declare))
            //    {
            //        //throw new InvalidOperationException($"Declaration of {_target} is not found");
            //    }

            //    declare.Type.Binding = new List<ColumnExpression>()
            //    {
            //        new() { Alias = "Name", Expression = new ScalarExpression() { Token = TokenType.String } },
            //        new() { Alias = "Type", Expression = new ScalarExpression() { Token = TokenType.String } },
            //        new() { Alias = "Value", Expression = new ScalarExpression() { Token = TokenType.Union } }
            //    };
            //}
            
            return property;
        }
        [Function("GET_PROPERTY")] public static DataObject GetProperty(this IScriptRuntime runtime, in DataObject record, in string name)
        {
            ArgumentNullException.ThrowIfNull(record);

            object value = record.GetValue(name);
            string type = (value is null) ? "NULL" : ParserHelper.GetDataTypeLiteral(value.GetType());

            DataObject property = new(3);
            property.SetValue("Name", name);
            property.SetValue("Type", type);
            property.SetValue("Value", value);

            return property;
        }

        [Function("ARRAY_COUNT")] public static int ArrayCount(this IScriptRuntime runtime, in List<DataObject> array)
        {
            if (array is null)
            {
                return -1;
            }

            return array.Count;
        }
        [Function("ARRAY_CLEAR")] public static int ArrayClear(this IScriptRuntime runtime, in List<DataObject> array)
        {
            if (array is null)
            {
                return -1;
            }

            array.Clear();

            return 0;
        }
        [Function("ARRAY_CREATE")] public static List<DataObject> ArrayCreate(this IScriptRuntime runtime)
        {
            return new(3);
        }
        [Function("ARRAY_CREATE")] public static List<DataObject> ArrayCreate(this IScriptRuntime runtime, int capacity)
        {
            return capacity <= 0 ? new(): new(capacity);
        }
        [Function("ARRAY_APPEND")] public static int ArrayAppend(this IScriptRuntime runtime, in List<DataObject> array, in DataObject record)
        {
            ArgumentNullException.ThrowIfNull(array, nameof(array));
            ArgumentNullException.ThrowIfNull(record, nameof(record));

            array.Add(record);

            return array.Count - 1;
        }
        [Function("ARRAY_SELECT")] public static DataObject ArraySelect(this IScriptRuntime runtime, in List<DataObject> array, int index)
        {
            ArgumentNullException.ThrowIfNull(array, nameof(array));

            return array[index];
        }
        [Function("ARRAY_DELETE")] public static DataObject ArrayDelete(this IScriptRuntime runtime, in List<DataObject> array, int index)
        {
            ArgumentNullException.ThrowIfNull(array, nameof(array));

            DataObject record = array[index];

            array.RemoveAt(index);

            return record;
        }
        [Function("ARRAY_INSERT")] public static int ArrayInsert(this IScriptRuntime runtime, in List<DataObject> array, int index, in DataObject record)
        {
            ArgumentNullException.ThrowIfNull(array, nameof(array));
            ArgumentNullException.ThrowIfNull(record, nameof(record));

            array.Insert(index, record);

            return array.Count;
        }

        [Function("OBJECT")] public static DataObject CreateObject(this IScriptRuntime runtime, in string metadataName)
        {
            if (runtime is not ScriptScope scope)
            {
                throw new ArgumentException("Parameter must be of type ScriptScope", nameof(runtime));
            }

            if (!scope.TryGetMetadataProvider(out IMetadataProvider provider, out string error))
            {
                throw new InvalidOperationException(error);
            }

            DataObject dataObject = provider.CreateObject(in metadataName);

            return dataObject;
        }
        [Function("OBJECT")] public static DataObject SelectObject(this IScriptRuntime runtime, in Entity key)
        {
            if (runtime is not ScriptScope scope)
            {
                throw new ArgumentException("Parameter must be of type ScriptScope", nameof(runtime));
            }

            if (!scope.TryGetMetadataProvider(out IMetadataProvider provider, out string error))
            {
                throw new InvalidOperationException(error);
            }

            DataObject dataObject = provider.SelectObject(in key);

            return dataObject;
        }
        [Function("OBJECT")] public static DataObject SelectObject(this IScriptRuntime runtime, in string metadataName, in Entity key)
        {
            if (runtime is not ScriptScope scope)
            {
                throw new ArgumentException("Parameter must be of type ScriptScope", nameof(runtime));
            }

            if (!scope.TryGetMetadataProvider(out IMetadataProvider provider, out string error))
            {
                throw new InvalidOperationException(error);
            }

            DataObject dataObject = provider.SelectObject(in metadataName, in key);

            return dataObject;
        }
        [Function("OBJECT")] public static DataObject SelectObject(this IScriptRuntime runtime, in string metadataName, in DataObject key)
        {
            if (runtime is not ScriptScope scope)
            {
                throw new ArgumentException("Parameter must be of type ScriptScope", nameof(runtime));
            }

            if (!scope.TryGetMetadataProvider(out IMetadataProvider provider, out string error))
            {
                throw new InvalidOperationException(error);
            }

            DataObject dataObject = provider.SelectObject(in metadataName, in key);

            return dataObject;
        }

        [Function("OBJECT")] public static DataObject SelectObject(this IScriptRuntime runtime, in Union union)
        {
            if (runtime is not ScriptScope scope)
            {
                throw new ArgumentException("Parameter must be of type ScriptScope", nameof(runtime));
            }

            if (!scope.TryGetMetadataProvider(out IMetadataProvider provider, out string error))
            {
                throw new InvalidOperationException(error);
            }

            DataObject dataObject = provider.SelectObject(union.GetEntity());

            return dataObject;
        }
    }
}