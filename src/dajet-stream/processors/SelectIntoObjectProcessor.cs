using DaJet.Data;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Data;
using System.Data.Common;

namespace DaJet.Stream
{
    public sealed class SelectIntoObjectProcessor : IProcessor
    {
        private IProcessor _next;
        private readonly StreamScope _scope;
        private Uri _uri;
        private VariableReference _into;
        private SqlStatement _statement;
        public SelectIntoObjectProcessor(in StreamScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            _uri = _scope.GetDatabaseUri();

            _statement = StreamFactory.Transpile(in _scope);
        }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Synchronize() { _next?.Synchronize(); }
        public void Dispose() { _next?.Dispose(); }
        public void Process()
        {
            IDbConnectionFactory factory = DbConnectionFactory.GetFactory(in _uri);

            int yearOffset = factory.GetYearOffset(in _uri);

            using (DbConnection connection = factory.Create(in _uri))
            {
                connection.Open();

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = _statement.Script;

                    PrepareDbParameters(in _statement, out Dictionary<string, object> parameters);

                    factory.ConfigureParameters(in command, in parameters, yearOffset);

                    using (IDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read()) // take the first record
                        {
                            DataObject record = new(_statement.Mapper.Properties.Count);

                            _statement.Mapper.Map(in reader, in record);

                            _ = _scope.TrySetValue(_into.Identifier, record);
                        }
                        reader.Close();
                    }
                }
            }

            _next?.Process();
        }
        private void PrepareDbParameters(in SqlStatement statement, out Dictionary<string, object> parameters)
        {
            parameters = new Dictionary<string, object>();

            List<VariableReference> variables = new VariableReferenceExtractor().Extract(statement.Node);

            foreach (VariableReference variable in variables)
            {
                if (variable.Binding is Type type || variable.Binding is Entity entity)
                {
                    // boolean, number, datetime, string, binary, uuid, entity

                    if (_scope.TryGetValue(variable.Identifier, out object value)) // @variable
                    {
                        if (parameters.ContainsKey(variable.Identifier))
                        {
                            parameters[variable.Identifier] = value;
                        }
                        else
                        {
                            parameters.Add(variable.Identifier, value);
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"Variable [{variable.Identifier}] is not found");
                    }
                }
                else if (variable.Binding is TypeIdentifier schema) // { array | object }
                {
                    if (schema.Token == TokenType.Array || schema.Token == TokenType.Object)
                    {
                        if (schema.Binding is List<ColumnExpression> columns)
                        {
                            // do nothing - INTO @variable clause or DaJet.Json(@object) function call
                        }
                    }
                }
            }

            List<MemberAccessExpression> expressions = new MemberAccessExtractor().Extract(statement.Node);

            foreach (MemberAccessExpression member in expressions)
            {
                string identifier = member.GetDbParameterName();

                if (_scope.TryGetValue(member.Identifier, out object value)) // @object.member
                {
                    if (parameters.ContainsKey(identifier))
                    {
                        parameters[identifier] = value;
                    }
                    else
                    {
                        parameters.Add(identifier, value);
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Member [{member.Identifier}] is not found");
                }
            }

            List<FunctionExpression> functions = new DaJetFunctionExtractor().Extract(statement.Node);

            foreach (FunctionExpression function in functions)
            {
                if (function.Name != "DaJet.Json")
                {
                    throw new InvalidOperationException($"Unknown function name: [{function.Name}]");
                }

                if (function.Parameters.Count == 0 ||
                    function.Parameters[0] is not VariableReference variable ||
                    variable.Binding is not TypeIdentifier type ||
                    type.Token != TokenType.Object)
                {
                    throw new InvalidOperationException($"Invalid parameter type: [{function.Name}]");
                }

                if (_scope.TryGetValue(variable.Identifier, out object value))
                {
                    if (value is DataObject record)
                    {
                        string json = StreamScope.ToJson(in record);

                        parameters.Add(function.GetVariableIdentifier(), json);// @dajet_json_variable
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Variable [{variable.Identifier}] is not found");
                }
            }

            //NOTE: the into variable might have been used by the code above

            if (statement.Node is SelectStatement select)
            {
                if (StreamFactory.TryGetIntoVariable(in select, out _into, out TokenType type))
                {
                    if (type == TokenType.Object)
                    {
                        _scope.TrySetValue(_into.Identifier, null);
                    }
                }
            }
        }
    }
}