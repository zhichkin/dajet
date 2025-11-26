using Google.Protobuf;
using Google.Protobuf.Reflection;
using System.Reflection;

namespace DaJet.Runtime.Kafka
{
    internal static class ProtobufConverter
    {
        private static Dictionary<string, Type> _messages = new();
        private static Dictionary<Type, object> _parsers = new();
        private static Dictionary<Type, MethodInfo> _from_protobuf = new();
        private static Dictionary<Type, PropertyInfo> _event_accessors = new();
        private static Dictionary<Type, MessageDescriptor> _descriptors = new();

        internal static byte[] ConvertJsonToProtobuf(in string message_type, in string message_body)
        {
            Type type = GetMessageType(in message_type);

            MessageDescriptor descriptor = GetDescriptor(in type);

            IMessage message = JsonParser.Default.Parse(message_body, descriptor);

            return message.ToByteArray();
        }
        private static Type GetMessageType(in string message_type)
        {
            if (_messages.TryGetValue(message_type, out Type message))
            {
                return message;
            }

            message = StreamFactory.AssemblyManager.Resolve(message_type);

            if (message is null)
            {
                throw new InvalidOperationException($"Message type is not found [{message_type}]");
            }

            _ = _messages.TryAdd(message_type, message);

            return message;
        }
        private static PropertyInfo GetEventAccessor(in Type message_type)
        {
            if (_event_accessors.TryGetValue(message_type, out PropertyInfo accessor))
            {
                return accessor;
            }

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.IgnoreCase;

            accessor = message_type.GetProperty("EventCase", flags);

            _ = _event_accessors.TryAdd(message_type, accessor);

            return accessor;
        }
        private static MessageDescriptor GetDescriptor(in Type message_type)
        {
            if (_descriptors.TryGetValue(message_type, out MessageDescriptor descriptor))
            {
                return descriptor;
            }

            BindingFlags flags = BindingFlags.Public | BindingFlags.Static;

            PropertyInfo property = message_type.GetProperty("Descriptor", flags);

            if (property is null)
            {
                throw new InvalidOperationException($"Message descriptor is not found [{message_type}]");
            }

            descriptor = property.GetValue(null) as MessageDescriptor;

            if (descriptor is null)
            {
                throw new InvalidOperationException($"Message descriptor is not found [{message_type}]");
            }

            _ = _descriptors.TryAdd(message_type, descriptor);

            return descriptor;
        }

        // https://docs.confluent.io/platform/current/schema-registry/fundamentals/serdes-develop/index.html#wire-format
        internal static string ConvertProtobufToJson(in string message_type, in byte[] message_body) // , out string event_type
        {
            //event_type = string.Empty;

            Type type = GetMessageType(in message_type);

            object parser = GetMessageParser(in type);

            MethodInfo method = GetParseFromMethod(in type);

            int offset = ParseSchemaRegistryHeader(in message_body);

            int length = message_body.Length - offset;

            if (method.Invoke(parser, [message_body, offset, length]) is not IMessage message)
            {
                return string.Empty;
            }

            //if (GetEventAccessor(in type) is PropertyInfo accessor)
            //{
            //    event_type = accessor.GetValue(message).ToString();
            //}

            string json = JsonFormatter.Default.Format(message);

            return json;
        }
        private static object GetMessageParser(in Type message_type)
        {
            object parser;

            if (_parsers.TryGetValue(message_type, out parser))
            {
                return parser;
            }

            BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.GetProperty;

            parser = message_type.InvokeMember("Parser", flags, null, null, null);

            if (parser is null)
            {
                throw new InvalidOperationException($"Message parser is not found [{message_type}]");
            }

            _ = _parsers.TryAdd(message_type, parser);

            return parser;
        }
        private static MethodInfo GetParseFromMethod(in Type message_type)
        {
            MethodInfo method;

            if (_from_protobuf.TryGetValue(message_type, out method))
            {
                return method;
            }

            object parser = GetMessageParser(in message_type);

            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

            Type[] parameters = new Type[] { typeof(byte[]), typeof(int), typeof(int) };

            method = parser.GetType().GetMethod("ParseFrom", flags, null, parameters, null);

            if (method is null)
            {
                throw new InvalidOperationException($"Method \"ParseFrom\" is not found [{message_type}]");
            }

            _ = _from_protobuf.TryAdd(message_type, method);

            return method;
        }
        private static int ParseSchemaRegistryHeader(in byte[] data)
        {
            if (data[0] > 0x00) // magic byte
            {
                return 0; // there is no Schema Registry header
            }

            // Schema Registry message schema identifier (4 bytes)

            int schema_id = BitConverter.ToInt32(data, 1);

            // message indexes: array length (varint) + message indexes (varint) = 1[0]
            // default case is 1[0] referencing root message (optimized to just 0x00):
            // - array   length  =  1
            // - message indexes = [0]

            if (data[5] == 0x00) // default case 1[0]
            {
                return 6; // data start offset
            }

            // read array length

            int index = 5;

            ulong value = ReadVarInt(in data, ref index);

            // read message indexes

            for (ulong i = 0; i < value; i++)
            {
                value = ReadVarInt(in data, ref index);
            }

            return index;
        }
        private static ulong ReadVarInt(in byte[] data, ref int index)
        {
            int offset = 0;
            ulong value = 0UL;
            ulong chunk;

            do
            {
                chunk = data[index++];
                value |= (chunk & 0x7F) << offset;
                offset += 7;
            }
            while ((chunk & 0x80) == 0x80);

            return value;
        }
    }
}