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
    public sealed class StreamScope
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
        public static string ToJson(in DataObject record)
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
        public static DataObject FromJson(in string json)
        {
            Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(json), true, default);

            return _converter.Read(ref reader, typeof(DataObject), JsonReaderOptions);
        }

        public StreamScope(SyntaxNode owner)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }
        public StreamScope(SyntaxNode owner, StreamScope parent) : this(owner)
        {
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
        }
        public SyntaxNode Owner { get; set; }
        public StreamScope Parent { get; set; }
        public List<StreamScope> Children { get; } = new();
        public override string ToString() { return $"Owner: {Owner}"; }
        public StreamScope GetRoot()
        {
            StreamScope root = this;

            while (root.Parent is not null)
            {
                root = root.Parent;
            }

            return root;
        }
        public StreamScope GetParent<TOwner>() where TOwner : SyntaxNode
        {
            Type type = typeof(TOwner);

            StreamScope scope = this;
            SyntaxNode owner = Owner;

            while (scope is not null)
            {
                if (owner is not null && owner.GetType() == type)
                {
                    return scope;
                }

                scope = scope.Parent;
                owner = scope?.Owner;
            }

            return null;
        }
        public StreamScope Open(in SyntaxNode owner)
        {
            StreamScope scope = new(owner, this);

            Children.Add(scope);

            return scope;
        }
        public StreamScope Close() { return Parent; }
        public static StreamScope Create(in ScriptModel script)
        {
            StreamScope scope = new(script);

            StreamScope _current = scope;

            for (int i = 0; i < script.Statements.Count; i++)
            {
                SyntaxNode statement = script.Statements[i];

                if (statement is CommentStatement) { continue; }

                if (statement is DeclareStatement declare)
                {
                    _current.Variables.Add(declare.Name, null);
                    _current.Declarations.Add(declare);
                }
                else if (IsStreamScope(in statement))
                {
                    if (_current.Owner is UseStatement && statement is UseStatement)
                    {
                        _current = _current.Close(); // one database context closes another
                    }
                    _current = _current.Open(in statement); // create parent scope
                }
                else
                {
                    _ = _current.Open(in statement); // add child to parent scope
                }
            }

            return scope;
        }
        public static bool IsStreamScope(in SyntaxNode statement)
        {
            return statement is UseStatement
                || statement is ForEachStatement
                || statement is ConsumeStatement
                || statement is ProduceStatement
                || statement is SelectStatement select && select.IsStream
                || statement is UpdateStatement update && update.Output?.Into?.Value is not null;
        }
        public List<DeclareStatement> Declarations { get; } = new(); // order is important for binding
        public Dictionary<string, object> Variables { get; } = new(); // scope variables and their values
        public bool TryGetValue(in string name, out object value)
        {
            value = null;

            string[] identifiers = name.Split('.');

            if (identifiers.Length == 1)
            {
                return TryGetScopeValue(in name, out value);
            }
            else if (identifiers.Length == 2)
            {
                if (TryGetScopeValue(identifiers[0], out value) && value is DataObject record)
                {
                    return record.TryGetValue(identifiers[1], out value);
                }
            }

            return false;
        }
        private bool TryGetScopeValue(in string name, out object value)
        {
            value = null;

            StreamScope scope = this;

            while (scope is not null)
            {
                if (scope.Variables.TryGetValue(name, out value))
                {
                    return true;
                }

                scope = scope.Parent;
            }

            return false;
        }
        public bool TryGetDeclaration(in string name, out bool local, out DeclareStatement declare)
        {
            local = true;
            declare = null;

            StreamScope scope = this;

            while (scope is not null)
            {
                for (int i = 0; i < scope.Declarations.Count; i++)
                {
                    declare = scope.Declarations[i];

                    if (declare.Name == name)
                    {
                        return true;
                    }
                }

                scope = scope.Parent;

                local = false;
            }

            return false;
        }

        private static readonly Regex _uri_template = new("{(.*?)}", RegexOptions.CultureInvariant);
        public Uri GetUri(in string uri)
        {
            string[] templates = GetUriTemplates(in uri);

            if (templates.Length == 0)
            {
                return new Uri(uri);
            }

            string result = string.Empty;

            for (int i = 0; i < templates.Length; i++)
            {
                string variable = templates[i].TrimStart('{').TrimEnd('}');

                if (TryGetValue(in variable, out object value))
                {
                    result = uri.Replace(templates[i], value.ToString());
                }
                else
                {
                    result = uri.Replace(templates[i], string.Empty);
                }
            }

            return new Uri(result);
        }
        public static string[] GetUriTemplates(in string uri)
        {
            MatchCollection matches = _uri_template.Matches(uri);

            if (matches.Count == 0)
            {
                return Array.Empty<string>();
            }

            string[] templates = new string[matches.Count];

            for (int i = 0; i < matches.Count; i++)
            {
                templates[i] = matches[i].Value;
            }

            return templates;
        }
        public static Uri GetUri(in string template, in Dictionary<string, string> values)
        {
            string uri = string.Empty;

            foreach (var item in values)
            {
                uri = template.Replace(item.Key, item.Value);
            }

            return new Uri(uri);
        }
        
        // ***

        private readonly Dictionary<string, object> _context;

        private string _uri;
        private string _intoArray;
        private string _intoObject;
        private Dictionary<string, MemberAccessDescriptor> _templates = new();
        private Dictionary<string, MemberAccessDescriptor> _descriptors = new();
        public StreamScope(in Dictionary<string, object> context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }
        public StreamScope Clone()
        {
            Dictionary<string, object> context = new();

            foreach (var variable in _context)
            {
                context.Add(variable.Key, variable.Value);
            }

            return new StreamScope(in context);
        }
        public void MapUri(in string uri)
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
        public Uri GetUri()
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
        public void SetIntoObject(in DataObject record)
        {
            if (_context.TryGetValue(_intoObject, out _))
            {
                _context[_intoObject] = record;
            }
        }
        public DataObject GetIntoObject()
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
            return null;
        }
        public List<DataObject> GetIntoArray()
        {
            if (_descriptors.TryGetValue(_intoArray, out MemberAccessDescriptor descriptor))
            {
                if (descriptor.TryGetValue(in _context, out object value))
                {
                    if (value is List<DataObject> table)
                    {
                        return table;
                    }
                }
            }
            return null;
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

        public void MapOptions(in ProduceStatement statement)
        {
            MapColumnExpressions(statement.Options);
            MapColumnExpressions(statement.Columns);
        }
        public void MapOptions(in ConsumeStatement statement)
        {
            MapColumnExpressions(statement.Options);
            MapColumnExpressions(statement.Columns);
        }
        public void MapIntoObject(in ConsumeStatement statement)
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
        public string GetExchange()
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
        public string GetRoutingKey()
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
        public bool GetMandatory()
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
        public string GetMessageBody()
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
        public string[] GetBlindCopy()
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
        public string[] GetCarbonCopy()
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
        public string GetAppId()
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
        public string GetMessageId()
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
        public string GetMessageType()
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
        public string GetCorrelationId()
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
        public byte GetPriority()
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
        public byte GetDeliveryMode()
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
        public string GetContentType()
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
        public string GetContentEncoding()
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
        public string GetReplyTo()
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
        public string GetExpiration()
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
        public string GetQueueName()
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
        public int GetHeartbeat()
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
        public uint GetPrefetchSize()
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
        public ushort GetPrefetchCount()
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
        #endregion

        #region "FOR EACH STATEMENT"
        public void MapIntoArray(in ForEachStatement statement)
        {
            _intoArray = statement.Iterator.Identifier;

            _descriptors.Add(_intoArray, new MemberAccessDescriptor()
            {
                Target = _intoArray,
                MemberType = typeof(Array)
            });
        }
        public void MapIntoObject(in ForEachStatement statement)
        {
            _intoObject = statement.Variable.Identifier;

            _descriptors.Add(_intoObject, new MemberAccessDescriptor()
            {
                Target = _intoObject,
                MemberType = typeof(object)
            });
        }
        #endregion
    }
}