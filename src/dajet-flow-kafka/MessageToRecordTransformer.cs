using Confluent.Kafka;
using DaJet.Data;
using Google.Protobuf;
using System.Data;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace DaJet.Flow.Kafka
{
    // https://protobuf.dev/getting-started/csharptutorial/
    [PipelineBlock] public sealed class MessageToRecordTransformer : TransformerBlock<ConsumeResult<byte[], byte[]>, IDataRecord>
    {
        private const string CONTENT_TYPE_PROTOBUF = "protobuf";

        private object _parser = null;
        private MethodInfo _parseFrom = null;
        private readonly object[] _parameters = new object[1];
        private readonly DataRecord _output = new();
        [Option] public string PackageName { get; set; } = string.Empty;
        [Option] public string MessageType { get; set; } = string.Empty;
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

            Type messageType = package.GetType(MessageType);

            if (messageType is null)
            {
                throw new InvalidOperationException($"Message type not found [{MessageType}]");
            }

            BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.GetProperty;
            _parser = messageType.InvokeMember("Parser", flags, null, null, null);

            if (_parser is null)
            {
                throw new InvalidOperationException($"Message parser not found [{MessageType}]");
            }

            flags = BindingFlags.Public | BindingFlags.Instance;
            _parseFrom = _parser.GetType().GetMethod("ParseFrom", flags, new Type[] { typeof(byte[]) });

            if (_parseFrom is null)
            {
                throw new InvalidOperationException($"Method \"ParseFrom\" not found [{MessageType}]");
            }
        }
        protected override void _Transform(in ConsumeResult<byte[], byte[]> input, out IDataRecord output)
        {
            if (ContentType != CONTENT_TYPE_PROTOBUF)
            {
                _output.SetValue("type", input.Topic);
                _output.SetValue("uuid", DeserializeKey(input.Message.Key));
                _output.SetValue("body", Encoding.UTF8.GetString(input.Message.Value));
                _output.SetValue("time", input.Message.Timestamp.UtcDateTime);
            }
            else
            {
                _parameters[0] = input.Message.Value;
                object instance = _parseFrom.Invoke(_parser, _parameters);

                if (instance is not IMessage message)
                {
                    _output.Clear();
                }
                else
                {
                    _output.SetValue("type", input.Topic);
                    _output.SetValue("uuid", DeserializeKey(input.Message.Key));
                    _output.SetValue("body", JsonFormatter.Default.Format(message));
                    _output.SetValue("time", input.Message.Timestamp.UtcDateTime);
                }
            }

            output = _output;
        }
        private Guid DeserializeKey(in byte[] key)
        {
            if (key is null || key.Length != 16)
            {
                return Guid.Empty;
            }
            return new Guid(key);
        }
        protected override void _Dispose()
        {
            _output.Clear();
            _parameters[0] = null;
        }
    }
}