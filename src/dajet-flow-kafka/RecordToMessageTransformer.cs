using Confluent.Kafka;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using System.Data;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace DaJet.Flow.Kafka
{
    // https://protobuf.dev/getting-started/csharptutorial/
    [PipelineBlock] public sealed class RecordToMessageTransformer : TransformerBlock<IDataRecord, Message<byte[], byte[]>>
    {
        private const string HEADER_TOPIC = "topic";
        private const string CONTENT_TYPE_PROTOBUF = "protobuf";

        private readonly Message<byte[], byte[]> _output = new();
        private readonly Dictionary<string, MessageDescriptor> _descriptors = new();
        [Option] public string PackageName { get; set; } = string.Empty;
        [Option] public string ContentType { get; set; } = string.Empty;
        protected override void _Configure()
        {
            if (string.IsNullOrWhiteSpace(ContentType) || ContentType != CONTENT_TYPE_PROTOBUF)
            {
                return;
            }

            Assembly package = null;

            foreach (Assembly assembly in AssemblyLoadContext.Default.Assemblies)
            {
                if (assembly.GetName().Name == PackageName)
                {
                    package = assembly; break;
                }
            }

            if (package is null)
            {
                throw new InvalidOperationException($"Package not found [{PackageName}]");
            }

            BindingFlags flags = BindingFlags.Public | BindingFlags.Static;

            foreach (Type type in package.GetTypes())
            {
                PropertyInfo property = type.GetProperty("Descriptor", flags);

                if (property is not null)
                {
                    object value = property.GetValue(null);

                    if (value is MessageDescriptor descriptor)
                    {
                        _ = _descriptors.TryAdd(type.FullName, descriptor);
                    }
                }
            }
        }
        protected override void _Transform(in IDataRecord input, out Message<byte[], byte[]> output)
        {
            _output.Key = null;
            _output.Value = null;
            _output.Headers = null;

            object key = null;
            string type = null;
            object body = null;
            string topic = null;

            for (int i = 0; i < input.FieldCount; i++)
            {
                string name = input.GetName(i);
                
                if (name == "КлючСообщения")
                {
                    if (!input.IsDBNull(i))
                    {
                        key = input.GetValue(i);
                    }
                }
                else if (name == "ТипСообщения")
                {
                    type = input.GetString(i);
                }
                else if (name == "ТелоСообщения")
                {
                    body = input.GetString(i);
                }
                else if (name == "Получатель")
                {
                    topic = input.GetString(i);
                }
            }

            _output.Key = SerializeKey(in key);
            _output.Value = SerializeValue(in type, in body);

            if (topic is not null)
            {
                _output.Headers = new Headers()
                {
                    { HEADER_TOPIC, Encoding.UTF8.GetBytes(topic) }
                };
            }

            output = _output;
        }
        protected override void _Dispose()
        {
            _output.Key = null;
            _output.Value = null;
            _output.Headers = null;
        }
        private byte[] SerializeKey(in object key)
        {
            if (key is null)
            {
                return null;
            }
            else if (key is Guid uuid)
            {
                return uuid.ToByteArray();
            }
            else if (key is string text && Guid.TryParse(text, out Guid guid))
            {
                return guid.ToByteArray();
            }
            else if (key is byte[] binary && binary.Length == 16)
            {
                return binary;
            }
            return null;
        }
        private byte[] SerializeValue(in string type, in object value)
        {
            if (value is not string json) { return null; }

            if (ContentType != CONTENT_TYPE_PROTOBUF)
            {
                return Encoding.UTF8.GetBytes(json);
            }
            
            if (!_descriptors.TryGetValue(type, out MessageDescriptor descriptor))
            {
                return null;
            }

            IMessage message = JsonParser.Default.Parse(json, descriptor);

            return message.ToByteArray();
        }
    }
}