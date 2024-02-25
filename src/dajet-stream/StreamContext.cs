using DaJet.Data;
using DaJet.Json;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using RabbitMQ.Client;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace DaJet.Stream
{
    internal sealed class StreamContext
    {
        private static readonly JsonWriterOptions JsonWriterOptions = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private static readonly JsonSerializerOptions JsonReaderOptions = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private static readonly DataObjectJsonConverter _converter = new();
        internal static string ToJson(in DataObject record)
        {
            using (MemoryStream memory = new())
            {
                using (Utf8JsonWriter writer = new(memory, JsonWriterOptions))
                {
                    _converter.Write(writer, record, null);

                    writer.Flush();

                    string json = Encoding.UTF8.GetString(memory.ToArray());

                    return json;
                }
            }
        }
        internal static DataObject FromJson(in string json)
        {
            Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(json), true, default);

            return _converter.Read(ref reader, typeof(DataObject), JsonReaderOptions);
        }

        private readonly Dictionary<string, object> _context;
        private string _uri;
        private string _intoObject;
        private Dictionary<string, MemberAccessDescriptor> _templates = new();
        private Dictionary<string, MemberAccessDescriptor> _descriptors = new();
        internal StreamContext(in Dictionary<string, object> context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }
        internal void MapUri(in string uri)
        {
            _uri = uri; // store initial value

            Regex template = new("{(.*?)}", RegexOptions.CultureInvariant);

            foreach (Match match in template.Matches(uri).Cast<Match>())
            {
                if (match.Value.Contains('.'))
                {
                    string[] identifier = match.Value.Split('.');
                    MemberAccessDescriptor descriptor = new()
                    {
                        Target = identifier[0],
                        Member = identifier[1]
                    };
                    _ = _templates.TryAdd(match.Value, descriptor);
                }
                else
                {
                    MemberAccessDescriptor descriptor = new()
                    {
                        Target = match.Value[1..] // script parameter
                    };
                    _ = _templates.TryAdd(match.Value, descriptor);
                }
            }
        }
        internal Uri GetUri()
        {
            string uri = string.Empty;

            foreach (var item in _templates)
            {
                if (item.Value.TryGetValue(in _context, out object value))
                {
                    uri = _uri.Replace(item.Key, value.ToString());
                }
            }

            return new Uri(uri);
        }
        internal void MapOptions(in ProduceStatement statement)
        {
            MapColumnExpressions(statement.Options);
            MapColumnExpressions(statement.Columns);
        }
        internal void MapOptions(in ConsumeStatement statement)
        {
            MapColumnExpressions(statement.Options);
            MapColumnExpressions(statement.Columns);
        }
        private void MapColumnExpressions(in List<ColumnExpression> columns)
        {
            foreach (ColumnExpression column in columns)
            {
                if (column.Expression is ScalarExpression scalar)
                {
                    _descriptors.Add(column.Alias, new MemberAccessDescriptor(scalar.Literal)
                    {
                        MemberType = ParserHelper.GetTokenDataType(scalar.Token)
                    });
                }
                else if (column.Expression is VariableReference variable && variable.Binding is Type type)
                {
                    string identifier = variable.Identifier[1..]; // simple type @variable

                    _descriptors.Add(column.Alias, new MemberAccessDescriptor()
                    {
                        Target = identifier,
                        MemberType = type
                    });
                }
                else if (column.Expression is MemberAccessExpression member)
                {
                    _descriptors.Add(column.Alias, member.ToDescriptor()); // simple type @variable.member
                }
                else if (column.Expression is FunctionExpression function
                    && function.Name == "DaJet.Json"
                    && function.Parameters.Count > 0
                    && function.Parameters[0] is VariableReference parameter
                    && parameter.Binding is TypeIdentifier schema)
                {
                    _descriptors.Add(column.Alias, new MemberAccessDescriptor()
                    {
                        Target = parameter.Identifier, // context @variable
                        MemberType = ParserHelper.GetTokenDataType(schema.Token) // object or Array
                    });
                }
            }
        }
        internal void MapIntoObject(in ConsumeStatement statement)
        {
            if (statement.Into is not null &&
                statement.Into.Value is VariableReference variable &&
                variable.Binding is TypeIdentifier type &&
                type.Token == TokenType.Object)
            {
                _intoObject = variable.Identifier;

                _descriptors.Add(_intoObject, new MemberAccessDescriptor()
                {
                    Target = _intoObject,
                    MemberType = typeof(object)
                });
            }
        }

        #region "PRODUCER OPTIONS"
        internal string GetExchange()
        {
            if (_descriptors.TryGetValue("Exchange", out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value))
                {
                    return value.ToString();
                }
            }
            return string.Empty;
        }
        internal string GetRoutingKey()
        {
            if (_descriptors.TryGetValue("RoutingKey", out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value))
                {
                    return value.ToString();
                }
            }
            return string.Empty;
        }
        internal bool GetMandatory()
        {
            if (_descriptors.TryGetValue("Mandatory", out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value))
                {
                    return value.ToString() == "true";
                }
            }
            return false;
        }
        internal string GetMessageBody()
        {
            if (_descriptors.TryGetValue("Body", out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value))
                {
                    if (descriptor.MemberType == typeof(object)) // DaJet.Json(@object) function call
                    {
                        if (value is DataObject record)
                        {
                            return ToJson(in record);
                        }
                    }
                    else
                    {
                        return value.ToString();
                    }
                }
            }
            return string.Empty;
        }
        internal string[] GetBlindCopy()
        {
            if (_descriptors.TryGetValue("BlindCopy", out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value))
                {
                    if (descriptor.MemberType == typeof(Array))
                    {
                        if (value is List<DataObject> list && list.Count > 0)
                        {
                            string[] result = new string[list.Count];

                            for (int i = 0; i < list.Count; i++)
                            {
                                result[i] = list[i].GetValue(0).ToString();
                            }

                            return result;
                        }
                    }
                    else
                    {
                        return value.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    }
                }
            }
            return null;
        }
        internal string[] GetCarbonCopy()
        {
            if (_descriptors.TryGetValue("CarbonCopy", out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value))
                {
                    if (descriptor.MemberType == typeof(Array))
                    {
                        if (value is List<DataObject> list && list.Count > 0)
                        {
                            string[] result = new string[list.Count];

                            for (int i = 0; i < list.Count; i++)
                            {
                                result[i] = list[i].GetValue(0).ToString();
                            }

                            return result;
                        }
                    }
                    else
                    {
                        return value.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    }
                }
            }
            return null;
        }
        internal string GetAppId()
        {
            if (_descriptors.TryGetValue(nameof(IBasicProperties.AppId), out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value))
                {
                    return value.ToString();
                }
            }
            return null;
        }
        internal string GetMessageId()
        {
            if (_descriptors.TryGetValue(nameof(IBasicProperties.MessageId), out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value))
                {
                    return value.ToString();
                }
            }
            return null;
        }
        internal string GetMessageType()
        {
            if (_descriptors.TryGetValue(nameof(IBasicProperties.Type), out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value))
                {
                    return value.ToString();
                }
            }
            return null;
        }
        internal string GetCorrelationId()
        {
            if (_descriptors.TryGetValue(nameof(IBasicProperties.CorrelationId), out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value))
                {
                    return value.ToString();
                }
            }
            return null;
        }
        internal byte GetPriority()
        {
            if (_descriptors.TryGetValue(nameof(IBasicProperties.Priority), out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value)
                    && value is not null
                    && byte.TryParse(value.ToString(), out byte result))
                {
                    return result;
                }
            }
            return 0;
        }
        internal byte GetDeliveryMode()
        {
            if (_descriptors.TryGetValue(nameof(IBasicProperties.DeliveryMode), out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value)
                    && value is not null
                    && byte.TryParse(value.ToString(), out byte result))
                {
                    return result;
                }
            }
            return 2;
        }
        internal string GetContentType()
        {
            if (_descriptors.TryGetValue(nameof(IBasicProperties.ContentType), out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value))
                {
                    return value.ToString();
                }
            }
            return "application/json";
        }
        internal string GetContentEncoding()
        {
            if (_descriptors.TryGetValue(nameof(IBasicProperties.ContentEncoding), out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value))
                {
                    return value.ToString();
                }
            }
            return "UTF-8";
        }
        internal string GetReplyTo()
        {
            if (_descriptors.TryGetValue(nameof(IBasicProperties.ReplyTo), out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value))
                {
                    return value.ToString();
                }
            }
            return null;
        }
        internal string GetExpiration()
        {
            if (_descriptors.TryGetValue(nameof(IBasicProperties.Expiration), out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value))
                {
                    return value.ToString();
                }
            }
            return null;
        }
        #endregion

        #region "CONSUMER OPTIONS"
        internal string GetQueueName()
        {
            if (_descriptors.TryGetValue("QueueName", out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value))
                {
                    return value.ToString();
                }
            }
            return string.Empty;
        }
        internal int GetHeartbeat()
        {
            if (_descriptors.TryGetValue("Heartbeat", out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value)
                    && value is not null
                    && int.TryParse(value.ToString(), out int result))
                {
                    return result;
                }
            }
            return 60; // seconds
        }
        internal uint GetPrefetchSize()
        {
            if (_descriptors.TryGetValue("PrefetchSize", out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value)
                    && value is not null
                    && uint.TryParse(value.ToString(), out uint result))
                {
                    return result;
                }
            }
            return 0; // size of the client buffer in bytes
        }
        internal ushort GetPrefetchCount()
        {
            if (_descriptors.TryGetValue("PrefetchCount", out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value)
                    && value is not null
                    && ushort.TryParse(value.ToString(), out ushort result))
                {
                    return result;
                }
            }
            return 1; // allowed messages on the fly without ack
        }
        internal DataObject GetIntoObject()
        {
            if (_descriptors.TryGetValue(_intoObject, out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value))
                {
                    if (value is DataObject record)
                    {
                        return record;
                    }
                }
            }

            DataObject message = new(8);

            message.SetValue("Body", string.Empty);
            message.SetValue(nameof(IBasicProperties.AppId), string.Empty);
            message.SetValue(nameof(IBasicProperties.Type), string.Empty);
            message.SetValue(nameof(IBasicProperties.ContentType), "application/json");
            message.SetValue(nameof(IBasicProperties.ContentEncoding), "UTF-8");
            message.SetValue(nameof(IBasicProperties.ReplyTo), string.Empty);
            message.SetValue(nameof(IBasicProperties.MessageId), string.Empty);
            message.SetValue(nameof(IBasicProperties.CorrelationId), string.Empty);

            return message;
        }
        #endregion
    }
}