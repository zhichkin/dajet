using DaJet.Data;
using DaJet.Json;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Globalization;
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

        [Function("NEWUUID")] public static Guid GenerateNewUuid(this IScriptRuntime _)
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

        #region "ENTITY"
        [Function("ENTITY")] public static Entity CreateEntity(this IScriptRuntime runtime, in string name)
        {
            return CreateEntity(runtime, in name, Guid.Empty);
        }
        [Function("ENTITY")] public static Entity CreateEntity(this IScriptRuntime runtime, int typeCode, Guid identity)
        {
            return new Entity(typeCode, identity);
        }
        [Function("ENTITY")] public static Entity CreateEntity(this IScriptRuntime runtime, long typeCode, Guid identity)
        {
            return new Entity((int)typeCode, identity);
        }
        [Function("ENTITY")] public static Entity CreateEntity(this IScriptRuntime runtime, decimal typeCode, Guid identity)
        {
            return new Entity(decimal.ToInt32(typeCode), identity);
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
        #endregion

        #region "ARRAY"
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
            return capacity <= 0 ? new() : new(capacity);
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
        #endregion

        #region "OBJECT"
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

        private static DataObject CreateObject(in TypeDefinition defintition)
        {
            DataObject dataObject = new(defintition.Properties.Count);

            foreach (PropertyDefinition property in defintition.Properties)
            {
                dataObject.SetValue(property.Name, ParserHelper.GetDefaultValue(property.Type));
            }

            return dataObject;
        }
        [Function("OBJECT")] public static DataObject CreateObject(this IScriptRuntime runtime, in string metadataName)
        {
            if (runtime is not ScriptScope scope)
            {
                throw new ArgumentException("Parameter must be of type ScriptScope", nameof(runtime));
            }

            if (scope.TryGetDefinition(metadataName, out TypeDefinition definition))
            {
                return CreateObject(in definition);
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
            //TODO: calling this method from DaJet Script does not work yet ...

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
        #endregion

        #region "UUID CONVERTION"
        [Function("UUID1C")] public static Guid CONVERT_UUID_DB_TO_1C(this IScriptRuntime runtime, Guid uuid)
        {
            return new Guid(DbUtilities.Get1CUuid(uuid));
        }
        [Function("UUID1C")] public static Guid CONVERT_UUID_DB_TO_1C(this IScriptRuntime runtime, in byte[] source)
        {
            if (source is null || source.Length != 16)
            {
                return Guid.Empty;
            }
            Guid uuid = new(source);
            return new Guid(DbUtilities.Get1CUuid(uuid));
        }
        [Function("UUID1C")] public static Guid CONVERT_UUID_DB_TO_1C(this IScriptRuntime runtime, in string source)
        {
            if (!Guid.TryParse(source, out Guid uuid))
            {
                return Guid.Empty;
            }
            return new Guid(DbUtilities.Get1CUuid(uuid));
        }
        [Function("UUID1C")] public static Guid CONVERT_UUID_DB_TO_1C(this IScriptRuntime runtime, in Entity entity)
        {
            Guid uuid = GetEntityIdentity(runtime, in entity);
            return new Guid(DbUtilities.Get1CUuid(uuid));
        }

        [Function("UUIDDB")] public static Guid CONVERT_UUID_1C_TO_DB(this IScriptRuntime _, Guid uuid)
        {
            return new Guid(DbUtilities.GetSqlUuid(uuid));
        }
        [Function("UUIDDB")] public static Guid CONVERT_UUID_1C_TO_DB(this IScriptRuntime _, in byte[] source)
        {
            if (source is null || source.Length != 16)
            {
                return Guid.Empty;
            }
            Guid uuid = new(source);
            return new Guid(DbUtilities.GetSqlUuid(uuid));
        }
        [Function("UUIDDB")] public static Guid CONVERT_UUID_1C_TO_DB(this IScriptRuntime _, in string source)
        {
            if (!Guid.TryParse(source, out Guid uuid))
            {
                return Guid.Empty;
            }
            return new Guid(DbUtilities.GetSqlUuid(uuid));
        }
        [Function("UUIDDB")] public static Guid CONVERT_UUID_1C_TO_DB(this IScriptRuntime runtime, in Entity entity)
        {
            Guid uuid = GetEntityIdentity(runtime, in entity);
            return new Guid(DbUtilities.GetSqlUuid(uuid));
        }
        #endregion

        #region "DATE & TIME"
        [Function("NOW")] public static DateTime GetCurrentDateTime(this IScriptRuntime _)
        {
            return DateTime.Now; // The resolution ranges from 0.5 to 15 ms, depending on the operating system.
        }
        [Function("NOW")] public static DateTime GetCurrentDateTime(this IScriptRuntime _, int adjustment)
        {
            DateTime now = DateTime.Now;
            now = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
            return now.AddSeconds(adjustment);
        }
        [Function("UTC")] public static DateTime GetCurrentUtcDateTime(this IScriptRuntime _)
        {
            return DateTime.UtcNow; // The same resolution as for the NOW function.
        }
        [Function("UTC")] public static DateTime GetCurrentUtcDateTime(this IScriptRuntime _, int timeZone)
        {
            DateTime utc = DateTime.UtcNow;
            utc = new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second);
            return utc.AddHours(timeZone);
        }
        #endregion

        #region "CAST"
        private static NumberFormatInfo _numberFormatter = new()
        {
            NumberDecimalSeparator = ".",
            NumberGroupSeparator = string.Empty
        };
        [Function("TYPEOF")] public static string GetUnionType(this IScriptRuntime _, in Union union)
        {
            if (union is null) { return "NULL"; }
            else if (union.Tag == UnionTag.Undefined) { return "undefined"; }
            else if (union.Tag == UnionTag.Boolean) { return "boolean"; }
            else if (union.Tag == UnionTag.Numeric) { return "number"; }
            else if (union.Tag == UnionTag.DateTime) { return "datetime"; }
            else if (union.Tag == UnionTag.String) { return "string"; }
            else if (union.Tag == UnionTag.Entity) { return "entity"; }

            throw new InvalidOperationException("The union value contains an invalid type.");
        }
        private static void ThrowTypeCastException(in string source, in string target)
        {
            string message = $"Error casting source type [{source}] to target type [{target}]";

            throw new InvalidCastException(message);
        }
        
        [Function("CAST")] public static object CastUnion(this IScriptRuntime _, in Union union, in TypeIdentifier type)
        {
            ArgumentNullException.ThrowIfNull(union);
            
            if (union.IsUndefined)
            {
                return null;
            }
            else if (type.Identifier == "boolean")
            {
                return CastUnionToBoolean(in union);
            }
            else if (type.Identifier == "integer")
            {
                return CastUnionToInteger(in union);
            }
            else if (type.Identifier == "number" || type.Identifier == "decimal")
            {
                return CastUnionToDecimal(in union);
            }
            else if (type.Identifier == "datetime")
            {
                return CastUnionToDateTime(in union);
            }
            else if (type.Identifier == "string")
            {
                return CastUnionToString(in union);
            }
            else if (type.Identifier == "entity")
            {
                return CastUnionToEntity(in union);
            }

            ThrowTypeCastException("union", type.Identifier);
            
            return null;
        }
        private static bool CastUnionToBoolean(in Union union)
        {
            ArgumentNullException.ThrowIfNull(union);

            if (union.Tag != UnionTag.Boolean)
            {
                ThrowTypeCastException("union", "boolean");
            }

            return union.GetBoolean();
        }
        private static decimal CastUnionToDecimal(in Union union)
        {
            ArgumentNullException.ThrowIfNull(union);

            if (union.Tag != UnionTag.Numeric)
            {
                ThrowTypeCastException("union", "decimal");
            }

            return union.GetNumeric();
        }
        private static int CastUnionToInteger(in Union union)
        {
            ArgumentNullException.ThrowIfNull(union);

            if (union.Tag != UnionTag.Numeric)
            {
                ThrowTypeCastException("union", "integer");
            }

            return decimal.ToInt32(union.GetNumeric());
        }
        private static DateTime CastUnionToDateTime(in Union union)
        {
            ArgumentNullException.ThrowIfNull(union);

            if (union.Tag != UnionTag.DateTime)
            {
                ThrowTypeCastException("union", "datetime");
            }

            return union.GetDateTime();
        }
        private static string CastUnionToString(in Union union)
        {
            ArgumentNullException.ThrowIfNull(union);

            if (union.Tag != UnionTag.String)
            {
                ThrowTypeCastException("union", "string");
            }

            return union.GetString();
        }
        private static Entity CastUnionToEntity(in Union union)
        {
            ArgumentNullException.ThrowIfNull(union);

            if (union.Tag != UnionTag.Entity)
            {
                ThrowTypeCastException("union", "entity");
            }

            return union.GetEntity();
        }

        [Function("CAST")] public static object CastBoolean(this IScriptRuntime _, in bool value, in TypeIdentifier type)
        {
            if (type.Identifier == "integer")
            {
                return value ? 1 : 0;
            }
            else if (type.Identifier == "string")
            {
                return value ? "true" : "false";
            }
            else if (type.Identifier == "binary")
            {
                return value ? new byte[] { 1 } : new byte[] { 0 };
            }

            ThrowTypeCastException("boolean", type.Identifier);
            
            return null;
        }
        [Function("CAST")] public static object CastInteger(this IScriptRuntime _, in int value, in TypeIdentifier type)
        {
            if (type.Identifier == "boolean")
            {
                return value != 0;
            }
            else if (type.Identifier == "datetime")
            {
                return DateTime.MinValue.AddSeconds(value);
            }
            else if (type.Identifier == "string")
            {
                return value.ToString(_numberFormatter);
            }
            else if (type.Identifier == "binary")
            {
                if (type.Qualifier1 == 1) // binary(1)
                {
                    return new byte[] { DbUtilities.GetByteArray(value)[3] };
                }
                else // binary(4)
                {
                    return DbUtilities.GetByteArray(value);
                }
            }

            ThrowTypeCastException("integer", type.Identifier);

            return null;
        }
        [Function("CAST")] public static object CastBigInteger(this IScriptRuntime _, in long value, in TypeIdentifier type)
        {
            if (type.Identifier == "boolean")
            {
                return value != 0L;
            }
            else if (type.Identifier == "datetime")
            {
                return DateTime.MinValue.AddSeconds(value);
            }
            else if (type.Identifier == "string")
            {
                return value.ToString(_numberFormatter);
            }
            else if (type.Identifier == "binary")
            {
                return DbUtilities.GetByteArray(value);
            }

            ThrowTypeCastException("integer", type.Identifier);

            return null;
        }
        [Function("CAST")] public static object CastDecimal(this IScriptRuntime _, in decimal value, in TypeIdentifier type)
        {
            if (type.Identifier == "boolean")
            {
                return value != 0M;
            }
            else if (type.Identifier == "datetime")
            {
                return DateTime.MinValue.AddSeconds(decimal.ToInt64(value));
            }
            else if (type.Identifier == "string")
            {
                return value.ToString(_numberFormatter);
            }
            else if (type.Identifier == "binary")
            {
                return decimal.GetBits(value); //TODO: convert to byte array
            }

            ThrowTypeCastException("decimal", type.Identifier);

            return null;
        }
        [Function("CAST")] public static object CastDateTime(this IScriptRuntime _, in DateTime value, in TypeIdentifier type)
        {
            if (type.Identifier == "integer")
            {
                return Convert.ToInt64(value.Subtract(DateTime.MinValue).TotalSeconds);
            }
            else if (type.Identifier == "string")
            {
                return value.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else if (type.Identifier == "binary")
            {
                return value.ToBinary(); //TODO: convert to byte array
            }

            ThrowTypeCastException("datetime", type.Identifier);

            return null;
        }
        [Function("CAST")] public static object CastString(this IScriptRuntime _, in string value, in TypeIdentifier type)
        {
            if (type.Identifier == "boolean")
            {
                return value == "true";
            }
            else if (type.Identifier == "integer")
            {
                return int.Parse(value); //TODO: parse to long on error
            }
            else if (type.Identifier == "decimal" || type.Identifier == "number")
            {
                return decimal.Parse(value);
            }
            else if (type.Identifier == "datetime")
            {
                return DateTime.Parse(value);
            }
            else if (type.Identifier == "string")
            {
                return value;
            }
            else if (type.Identifier == "binary")
            {
                return Encoding.UTF8.GetBytes(value);
            }
            else if (type.Identifier == "uuid")
            {
                return new Guid(value);
            }
            else if (type.Identifier == "entity")
            {
                return Entity.Parse(value);
            }

            ThrowTypeCastException("string", type.Identifier);

            return null;
        }
        [Function("CAST")] public static object CastBinary(this IScriptRuntime _, in byte[] value, in TypeIdentifier type)
        {
            if (type.Identifier == "string")
            {
                return "0x" + DbUtilities.ByteArrayToString(value);
            }

            ThrowTypeCastException("binary", type.Identifier);

            return null;
        }
        [Function("CAST")] public static object CastUuid(this IScriptRuntime _, in Guid value, in TypeIdentifier type)
        {
            if (type.Identifier == "string")
            {
                return value.ToString(); //TODO formatting !?
            }
            else if (type.Identifier == "binary")
            {
                return value.ToByteArray();
            }
            
            ThrowTypeCastException("uuid", type.Identifier);

            return null;
        }
        [Function("CAST")] public static object CastEntity(this IScriptRuntime _, in Entity value, in TypeIdentifier type)
        {
            if (type.Identifier == "string")
            {
                return value.ToString();
            }
            else if (type.Identifier == "uuid")
            {
                return value.Identity;
            }
            else if (type.Identifier == "integer")
            {
                return value.TypeCode;
            }

            ThrowTypeCastException("entity", type.Identifier);

            return null;
        }
        #endregion
    }
}