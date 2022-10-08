using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MDLP
{
    public sealed class FlexibleJsonConverter : JsonConverter<FlexibleJsonObject>
    {
        public override FlexibleJsonObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
        public override void Write(Utf8JsonWriter writer, FlexibleJsonObject value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        private readonly IReferenceResolver _resolver;
        private readonly ISerializationBinder _binder;
        public FlexibleJsonConverter(ISerializationBinder binder, IReferenceResolver resolver) : base()
        {
            _binder = binder;
            _resolver = resolver;
        }
        //    public override bool CanConvert(Type typeToConvert)
        //    {
        //        if (typeToConvert == null) return false;
        //        return typeToConvert.IsAssignableFrom(typeof(FlexibleJsonObject));
        //    }
        //    public override void Write(Utf8JsonWriter writer, FlexibleJsonObject value, JsonSerializerOptions options)
        //    {
        //        bool isNew = false;
        //        string id = _resolver.GetReference(value, ref isNew);

        //        if (!isNew)
        //        {
        //            writer.WriteStartObject();
        //            writer.WriteString("$ref", id);
        //            writer.WriteEndObject();
        //            return;
        //        }

        //        Type type = value.GetType();
        //        writer.WriteStartObject();
        //        writer.WriteString("$id", id);
        //        writer.WriteString("$type", _binder.GetTypeCode(type));
        //        foreach (PropertyInfo info in type.GetProperties())
        //        {
        //            WriteProperty(writer, value, info, options);
        //        }
        //        writer.WriteEndObject();
        //    }
        //    private void WriteProperty(Utf8JsonWriter writer, FlexibleJsonObject source, PropertyInfo info, JsonSerializerOptions options)
        //    {
        //        if (info.IsOptional())
        //        {
        //            WriteOption(writer, source, info, options);
        //        }
        //        else
        //        {
        //            object value = info.GetValue(source);
        //            writer.WritePropertyName(info.Name);

        //            if (info.IsRepeatable())
        //            {
        //                WriteArray(writer, (IEnumerable)value, options);
        //            }
        //            else if (value is FlexibleJsonObject)
        //            {
        //                Write(writer, (FlexibleJsonObject)value, options);
        //            }
        //            else if (value is Assembly)
        //            {
        //                JsonSerializer.Serialize(writer, value.ToString(), typeof(string));
        //            }
        //            else if (value is Type)
        //            {
        //                JsonSerializer.Serialize(writer, ((Type)value).AssemblyQualifiedName, typeof(string));
        //            }
        //            else
        //            {
        //                JsonSerializer.Serialize(writer, value, info.PropertyType);
        //            }
        //        }
        //    }
        //    private void WriteOption(Utf8JsonWriter writer, FlexibleJsonObject source, PropertyInfo info, JsonSerializerOptions options)
        //    {
        //        writer.WritePropertyName(info.Name);

        //        writer.WriteStartObject();

        //        IOptional optional = (IOptional)info.GetValue(source);
        //        writer.WriteBoolean("HasValue", optional.HasValue);

        //        if (!optional.HasValue)
        //        {
        //            writer.WriteNull("Value");
        //            writer.WriteEndObject();
        //            return;
        //        }

        //        object value = optional.Value;
        //        Type propertyType = info.PropertyType.GetGenericArguments()[0];

        //        writer.WritePropertyName("Value");

        //        if (value == null)
        //        {
        //            JsonSerializer.Serialize(writer, value, propertyType);
        //        }
        //        else if (info.IsRepeatable())
        //        {
        //            WriteArray(writer, (IEnumerable)value, options);
        //        }
        //        else if (value is FlexibleJsonObject)
        //        {
        //            Write(writer, (FlexibleJsonObject)value, options);
        //        }
        //        else if (value is Assembly)
        //        {
        //            JsonSerializer.Serialize(writer, ((Assembly)value).FullName, typeof(string));
        //        }
        //        else if (value is Type)
        //        {
        //            JsonSerializer.Serialize(writer, ((Type)value).AssemblyQualifiedName, typeof(string));
        //        }
        //        else
        //        {
        //            JsonSerializer.Serialize(writer, value, value.GetType());
        //        }

        //        writer.WriteEndObject();
        //    }
        //    private void WriteArray(Utf8JsonWriter writer, IEnumerable list, JsonSerializerOptions options)
        //    {
        //        writer.WriteStartArray();
        //        IEnumerator enumerator = list.GetEnumerator();
        //        while (enumerator.MoveNext())
        //        {
        //            if (enumerator.Current is FlexibleJsonObject entity)
        //            {
        //                Write(writer, entity, options);
        //            }
        //            else
        //            {
        //                JsonSerializer.Serialize(writer, enumerator.Current, enumerator.Current.GetType());
        //            }
        //        }
        //        writer.WriteEndArray();
        //    }

        //    public override FlexibleJsonObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        //    {
        //        return ReadObject(ref reader, options);
        //    }
        //    private FlexibleJsonObject ReadObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
        //    {
        //        byte[] ID = Encoding.UTF8.GetBytes("$id");
        //        byte[] REF = Encoding.UTF8.GetBytes("$ref");
        //        byte[] TYPE = Encoding.UTF8.GetBytes("$type");

        //        string reference1 = string.Empty;
        //        string propertyName = string.Empty;
        //        PropertyInfo propertyInfo = null;

        //        FlexibleJsonObject entity = null;
        //        Type entityType = null;

        //        while (reader.Read())
        //        {
        //            if (reader.TokenType == JsonTokenType.StartObject)
        //            {
        //                if (propertyInfo.IsOptional())
        //                {
        //                    IOptional optional = (IOptional)propertyInfo.GetValue(entity);
        //                    ReadOption(ref reader, optional, options);
        //                }
        //                else
        //                {
        //                    FlexibleJsonObject value = ReadObject(ref reader, options);
        //                    propertyInfo.SetValue(entity, value);
        //                }
        //            }
        //            else if (reader.TokenType == JsonTokenType.EndObject)
        //            {
        //                return entity;
        //            }
        //            else if (reader.TokenType == JsonTokenType.PropertyName)
        //            {
        //                if (reader.ValueTextEquals(REF))
        //                {
        //                    reader.Read();
        //                    string referenceN = reader.GetString();
        //                    entity = (FlexibleJsonObject)_resolver.ResolveReference(referenceN);
        //                    while (reader.TokenType != JsonTokenType.EndObject)
        //                    {
        //                        if (!reader.Read()) { break; }
        //                    }
        //                    return entity;
        //                }
        //                else if (reader.ValueTextEquals(ID))
        //                {
        //                    reader.Read();
        //                    reference1 = reader.GetString();
        //                }
        //                else if (reader.ValueTextEquals(TYPE))
        //                {
        //                    reader.Read();
        //                    string typeCode = reader.GetString();
        //                    entityType = _binder.GetType(typeCode);
        //                    entity = (FlexibleJsonObject)Activator.CreateInstance(entityType);
        //                    _resolver.AddReference(reference1, entity);
        //                }
        //                else
        //                {
        //                    propertyName = reader.GetString();
        //                    propertyInfo = entityType.GetProperty(propertyName);
        //                }
        //            }
        //            else if (reader.TokenType == JsonTokenType.Null)
        //            {
        //                propertyInfo.SetValue(entity, null);
        //            }
        //            else if (reader.TokenType == JsonTokenType.True)
        //            {
        //                propertyInfo.SetValue(entity, true);
        //            }
        //            else if (reader.TokenType == JsonTokenType.False)
        //            {
        //                propertyInfo.SetValue(entity, false);
        //            }
        //            else if (reader.TokenType == JsonTokenType.Number)
        //            {
        //                uint intValue = reader.GetUInt32();
        //                if (propertyInfo.PropertyType.IsEnum)
        //                {
        //                    propertyInfo.SetValue(entity, Enum.GetValues(propertyInfo.PropertyType).GetValue(intValue));
        //                }
        //                else
        //                {
        //                    propertyInfo.SetValue(entity, intValue);
        //                }
        //            }
        //            else if (reader.TokenType == JsonTokenType.String)
        //            {
        //                string stringValue = reader.GetString();
        //                if (propertyInfo.PropertyType == typeof(Type))
        //                {
        //                    propertyInfo.SetValue(entity, Type.GetType(stringValue));
        //                }
        //                else if (propertyInfo.PropertyType == typeof(Assembly))
        //                {
        //                    //AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name == name);
        //                    propertyInfo.SetValue(entity, Assembly.Load(stringValue));
        //                }
        //                else
        //                {
        //                    propertyInfo.SetValue(entity, stringValue);
        //                }
        //            }
        //            else if (reader.TokenType == JsonTokenType.StartArray)
        //            {
        //                IList list = (IList)propertyInfo.GetValue(entity);
        //                while (reader.TokenType != JsonTokenType.EndArray)
        //                {
        //                    if (!reader.Read() || reader.TokenType == JsonTokenType.EndArray)
        //                    {
        //                        break;
        //                    }
        //                    FlexibleJsonObject item = ReadObject(ref reader, options);
        //                    list.Add(item);
        //                }
        //            }
        //        }
        //        return entity; // never gets here - JsonTokenType.EndObject returns ...
        //    }
        //    private void ReadOption(ref Utf8JsonReader reader, IOptional target, JsonSerializerOptions options)
        //    {
        //        reader.Read(); // read property name
        //        if (reader.GetString() == "HasValue")
        //        {
        //            reader.Read(); // read property value
        //            target.HasValue = reader.GetBoolean();
        //        }
        //        if (!target.HasValue)
        //        {
        //            reader.Read(); // property name "Value"
        //            reader.Read(); // property "Value" null value
        //            reader.Read(); // end of Optional<T> object
        //            return;
        //        }

        //        PropertyInfo propertyInfo = null;

        //        while (reader.Read())
        //        {
        //            if (reader.TokenType == JsonTokenType.StartObject)
        //            {
        //                target.Value = ReadObject(ref reader, options);
        //            }
        //            else if (reader.TokenType == JsonTokenType.EndObject)
        //            {
        //                return;
        //            }
        //            else if (reader.TokenType == JsonTokenType.PropertyName)
        //            {
        //                string propertyName = reader.GetString(); // must be "Value"
        //                propertyInfo = target.GetType().GetProperty(propertyName);
        //            }
        //            else if (reader.TokenType == JsonTokenType.Null)
        //            {
        //                target.Value = null;
        //            }
        //            else if (reader.TokenType == JsonTokenType.True)
        //            {
        //                target.Value = true;
        //            }
        //            else if (reader.TokenType == JsonTokenType.False)
        //            {
        //                target.Value = false;
        //            }
        //            else if (reader.TokenType == JsonTokenType.Number)
        //            {
        //                uint intValue = reader.GetUInt32();
        //                if (propertyInfo.PropertyType.IsEnum)
        //                {
        //                    target.Value = Enum.GetValues(propertyInfo.PropertyType).GetValue(intValue);
        //                }
        //                else
        //                {
        //                    target.Value = intValue;
        //                }
        //            }
        //            else if (reader.TokenType == JsonTokenType.String)
        //            {
        //                string stringValue = reader.GetString();
        //                if (propertyInfo.PropertyType == typeof(Type))
        //                {
        //                    target.Value = Type.GetType(stringValue);
        //                }
        //                else if (propertyInfo.PropertyType == typeof(Assembly))
        //                {
        //                    //AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name == name);
        //                    target.Value = Assembly.Load(stringValue);
        //                }
        //                else
        //                {
        //                    target.Value = stringValue;
        //                }
        //            }
        //            else if (reader.TokenType == JsonTokenType.StartArray)
        //            {
        //                IList list = (IList)propertyInfo.GetValue(target);
        //                if (list == null)
        //                {
        //                    list = (IList)propertyInfo.PropertyType.GetConstructor(Type.EmptyTypes).Invoke(null);
        //                    propertyInfo.SetValue(target, list);
        //                }
        //                while (reader.TokenType != JsonTokenType.EndArray)
        //                {
        //                    if (!reader.Read() || reader.TokenType == JsonTokenType.EndArray)
        //                    {
        //                        break;
        //                    }
        //                    FlexibleJsonObject item = ReadObject(ref reader, options);
        //                    list.Add(item);
        //                }
        //            }
        //        }
        //    }
    }
}