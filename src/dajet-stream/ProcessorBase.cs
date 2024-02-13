using DaJet.Data;
using DaJet.Data.Client;
using DaJet.Json;
using DaJet.Metadata;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Stream
{
    internal abstract class ProcessorBase : IProcessor
    {
        protected IProcessor _next;
        protected OneDbConnection _connection;
        protected readonly SqlStatement _statement;
        protected readonly IMetadataProvider _context;
        protected Dictionary<string, object> _parameters;
        protected string _arrayName;
        protected string _objectName;
        protected List<string> _memberNames = new();
        protected List<string> _parameterNames = new();
        protected MemberAccessDescriptor _descriptor;
        protected List<MemberAccessDescriptor> _functions = new();
        protected StatementType _mode = StatementType.Processor;

        private static readonly JsonWriterOptions JsonOptions = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private static readonly DataObjectJsonConverter _converter = new();

        internal ProcessorBase(in IMetadataProvider context, in SqlStatement statement, in Dictionary<string, object> parameters)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _statement = statement ?? throw new ArgumentNullException(nameof(statement));
            _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));

            _connection = new OneDbConnection(_context);

            Configure();
        }
        public void LinkTo(in IProcessor next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }
        public abstract void Process();
        public abstract void Synchronize();
        internal string ObjectName { get { return _objectName; } }
        private StatementType GetStatementType(in SqlStatement statement, out VariableReference into)
        {
            into = null;

            if (statement.Node is ForEachStatement)
            {
                return StatementType.Parallelizer;
            }
            else if (statement.Node is ConsumeStatement consume)
            {
                into = consume.Into.Value;

                return StatementType.Streaming;
            }
            else if (statement.Node is SelectStatement node)
            {
                if (node.Expression is SelectExpression select)
                {
                    return GetStatementType(in select, out into);
                }
                else if (node.Expression is TableUnionOperator union)
                {
                    return GetStatementType(in union, out into);
                }
            }
            else if (statement.Node is UpdateStatement update)
            {
                return GetStatementType(in update, out into);
            }

            return StatementType.Processor;
        }
        private StatementType GetStatementType(in SelectExpression node, out VariableReference into)
        {
            into = null;

            if (node.Into is not null &&
                node.Into.Value is VariableReference variable &&
                variable.Binding is TypeIdentifier type)
            {
                into = variable;

                if (type.Token == TokenType.Array)
                {
                    return StatementType.Buffering;
                }
                else if (type.Token == TokenType.Object)
                {
                    return StatementType.Streaming;
                }
            }

            return StatementType.Processor;
        }
        private StatementType GetStatementType(in TableUnionOperator node, out VariableReference into)
        {
            into = null;

            if (node.Expression1 is SelectExpression select)
            {
                return GetStatementType(in select, out into);
            }

            return StatementType.Processor;
        }
        private StatementType GetStatementType(in UpdateStatement node, out VariableReference into)
        {
            into = null;

            if (node.Output is not null &&
                node.Output.Into is not null &&
                node.Output.Into.Value is VariableReference variable &&
                variable.Binding is TypeIdentifier type)
            {
                into = variable;

                if (type.Token == TokenType.Array)
                {
                    return StatementType.Buffering;
                }
                else if (type.Token == TokenType.Object)
                {
                    return StatementType.Streaming;
                }
            }

            return StatementType.Processor;
        }
        private void Configure()
        {
            if (_statement.Node is SelectStatement statement &&
                statement.Expression is SelectExpression select &&
                select.Binding is MemberAccessDescriptor descriptor)
            {
                _descriptor = descriptor; // APPEND operator
            }

            _mode = GetStatementType(in _statement, out VariableReference into);

            if (into is not null)
            {
                if (_mode == StatementType.Buffering)
                {
                    _arrayName = into.Identifier;
                }
                else if (_mode == StatementType.Streaming)
                {
                    _objectName = into.Identifier;
                }

                if (_mode == StatementType.Streaming) // INTO @object
                {
                    if (!_parameters.ContainsKey(into.Identifier))
                    {
                        _parameters.Add(into.Identifier, null);
                    }
                }
                else if (_mode == StatementType.Buffering && _descriptor is null) // is not APPEND
                {
                    if (!_parameters.ContainsKey(into.Identifier))
                    {
                        _parameters.Add(into.Identifier, null);
                    }
                }
            }

            List<VariableReference> variables = new VariableReferenceExtractor().Extract(_statement.Node);

            foreach (VariableReference variable in variables)
            {
                if (variable.Binding is not TypeIdentifier type) // array or object
                {
                    _parameterNames.Add(variable.Identifier); //TODO: deduplicate !!!
                }
            }

            List<MemberAccessExpression> expressions = new MemberAccessExtractor().Extract(_statement.Node);

            foreach (MemberAccessExpression expression in expressions)
            {
                string[] identifier = expression.Identifier.Split('.');
                string target = identifier[0];
                string member = identifier[1];
                string memberName = $"{target}_{member}";

                if (!_parameters.ContainsKey(memberName))
                {
                    _parameters.Add(memberName, null);
                }

                _memberNames.Add(memberName); //TODO: deduplicate !!!
            }

            ConfigureFunctions();
        }
        private void ConfigureFunctions()
        {
            List<FunctionExpression> functions = new DaJetFunctionExtractor().Extract(_statement.Node);

            foreach (FunctionExpression function in functions)
            {
                if (function.Name != "DaJet.Json")
                {
                    throw new InvalidOperationException($"Unknown function name: [{function.Name}]");
                }

                string identifier = function.GetVariableIdentifier();

                if (!_parameters.ContainsKey(identifier))
                {
                    _parameters.Add(identifier, null);
                }

                if (function.Parameters.Count == 0 ||
                    function.Parameters[0] is not VariableReference variable)
                {
                    continue; //TODO: invalid parameter type error
                }

                MemberAccessDescriptor descriptor = new()
                {
                    Target = identifier, // output - query parameter
                    Member = variable.Identifier, // input - object variable
                };


                if (variable.Binding is TypeIdentifier type)
                {
                    if (type.Token == TokenType.Array)
                    {
                        descriptor.MemberType = typeof(Array);
                    }
                    else if (type.Token == TokenType.Object)
                    {
                        descriptor.MemberType = typeof(object);
                    }
                    else
                    {
                        //TODO: invalid parameter type error
                    }
                }

                _functions.Add(descriptor);
            }
        }
        private void ApplyFunction(in MemberAccessDescriptor descriptor, in Dictionary<string, object> parameters)
        {
            if (_parameters.TryGetValue(descriptor.Member, out object value))
            {
                if (value is DataObject record)
                {
                    string json = ToJson(in record);
                    
                    parameters.Add(descriptor.Target, json);
                }
            }
        }
        private string ToJson(in DataObject record)
        {
            using (MemoryStream memory = new())
            {
                using (Utf8JsonWriter writer = new(memory, JsonOptions))
                {
                    _converter.Write(writer, record, null);

                    writer.Flush();

                    string json = Encoding.UTF8.GetString(memory.ToArray());

                    return json;
                }
            }
        }
        private DataObject FromJson(in string json)
        {
            Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(json), true, default);

            return _converter.Read(ref reader, typeof(DataObject),
                new JsonSerializerOptions()
                {
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                });
        }
        protected void ConfigureParameters(in OneDbCommand command)
        {
            Dictionary<string, object> parameters = new();

            foreach (string memberName in _memberNames)
            {
                string[] identifier = memberName.Split('_');
                string target = identifier[0];
                string member = identifier[1];

                if (_parameters.TryGetValue(target, out object value))
                {
                    if (value is DataObject record)
                    {
                        if (record.TryGetValue(member, out value))
                        {
                            parameters.Add(memberName, value);
                        }
                    }
                }
            }

            foreach (MemberAccessDescriptor function in _functions)
            {
                ApplyFunction(in function, in parameters);
            }

            _context.ConfigureDbParameters(in parameters);

            //TODO: fix parameters naming ?

            foreach (var parameter in parameters)
            {
                //TODO: remove diagnostic !?
                //_pipeline.Parameters[parameter.Key] = parameter.Value;
                command.Parameters.Add(parameter.Key[1..], parameter.Value);
            }

            foreach (string parameterName in _parameterNames)
            {
                if (_parameters.TryGetValue(parameterName[1..], out object value))
                {
                    command.Parameters.Add(parameterName[1..], value);
                }
            }
        }
    }
}