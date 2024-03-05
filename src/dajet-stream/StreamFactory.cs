using DaJet.Data;
using DaJet.Metadata;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Data;

namespace DaJet.Stream
{
    internal static class StreamFactory
    {
        internal static IProcessor CreateAppendProcessor(
            in IMetadataProvider context, in Dictionary<string, object> parameters,
            in TableJoinOperator append, in string objectName)
        {
            if (append is null) { return null; }

            if (append.Token == TokenType.APPEND &&
                append.Expression2 is TableExpression subquery &&
                subquery.Expression is SelectExpression select &&
                select.Binding is MemberAccessDescriptor descriptor)
            {
                descriptor.Target = objectName;

                select.Into = new IntoClause()
                {
                    Columns = select.Columns,
                    Value = new VariableReference()
                    {
                        Identifier = $"{objectName}_{subquery.Alias}",
                        Binding = new TypeIdentifier()
                        {
                            Token = descriptor.MemberType == typeof(Array)
                            ? TokenType.Array
                            : TokenType.Object,
                            Binding = descriptor.MemberType,
                            Identifier = ParserHelper.GetDataTypeLiteral(descriptor.MemberType)
                        }
                    }
                };

                ScriptModel script = new()
                {
                    Statements = { new SelectStatement() { Expression = select } }
                };

                ISqlTranspiler transpiler;

                if (context.DatabaseProvider == DatabaseProvider.SqlServer)
                {
                    transpiler = new MsSqlTranspiler() { YearOffset = context.YearOffset };
                }
                else if (context.DatabaseProvider == DatabaseProvider.PostgreSql)
                {
                    transpiler = new PgSqlTranspiler() { YearOffset = context.YearOffset };
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported database provider: {context.DatabaseProvider}");
                }

                if (!transpiler.TryTranspile(in script, in context, out TranspilerResult result, out string error))
                {
                    throw new InvalidOperationException(error);
                }

                if (result.Statements.Count > 0)
                {
                    SqlStatement statement = result.Statements[0];

                    //TODO: return new Processor(in context, in statement, in parameters);
                }
            }

            return null;
        }
        
        // ***

        internal static IProcessor Create(in ScriptModel script)
        {
            StreamScope scope = StreamScope.Create(in script);

            return new DataStream(in scope);
        }
        internal static IProcessor CreateStream(in StreamScope parent)
        {
            StreamScope next;
            IProcessor starter = null;
            IProcessor current = null;
            IProcessor processor;

            for (int i = 0; i < parent.Children.Count; i++)
            {
                next = parent.Children[i];

                processor = CreateProcessor(in next);

                if (i == 0)
                {
                    starter = processor;
                }
                else
                {
                    current.LinkTo(in processor);
                }

                current = processor;
            }

            return starter;
        }
        internal static IProcessor CreateProcessor(in StreamScope scope)
        {
            if (scope.Owner is UseStatement)
            {
                return new UseProcessor(in scope);
            }
            else if (scope.Owner is ForEachStatement)
            {
                return new Parallelizer(in scope);
            }
            else if (scope.Owner is ConsumeStatement consume)
            {
                if (string.IsNullOrEmpty(consume.Target))
                {
                    return new ConsumeProcessor(in scope); // CONSUME
                }
                else
                {
                    return CreateMessageBrokerProcessor(in scope);
                }
            }
            else if (scope.Owner is ProduceStatement)
            {
                return CreateMessageBrokerProcessor(in scope);
            }
            else if (scope.Owner is SelectStatement select && select.IsStream) // STREAM
            {
                return new StreamProcessor(in scope);
            }
            
            return CreateDatabaseProcessor(in scope);
        }
        internal static IProcessor CreateDatabaseProcessor(in StreamScope scope)
        {
            if (TryGetIntoVariable(scope.Owner, out VariableReference variable))
            {
                if (scope.TryGetDeclaration(variable.Identifier, out _, out DeclareStatement declare))
                {
                    if (declare.Type.Token == TokenType.Array)
                    {
                        return new IntoArrayProcessor(in scope);
                    }
                    else if (declare.Type.Token == TokenType.Object)
                    {
                        return new IntoObjectProcessor(in scope);
                    }
                }
            }
            
            return new NonQueryProcessor(in scope);
        }
        internal static bool TryGetIntoVariable(in SyntaxNode node, out VariableReference into)
        {
            into = null;

            if (node is SelectStatement select)
            {
                return TryGetIntoVariable(in select, out into);
            }
            else if (node is UpdateStatement update)
            {
                return TryGetIntoVariable(in update, out into);
            }

            return into is not null;
        }
        internal static bool TryGetIntoVariable(in SelectStatement statement, out VariableReference into)
        {
            if (statement.Expression is SelectExpression select)
            {
                return TryGetIntoVariable(in select, out into);
            }
            else if (statement.Expression is TableUnionOperator union)
            {
                return TryGetIntoVariable(in union, out into);
            }

            into = null;
            return false;
        }
        private static bool TryGetIntoVariable(in SelectExpression select, out VariableReference into)
        {
            into = null;

            if (select.Into?.Value is VariableReference variable)
            {
                into = variable;
            }

            return into is not null;
        }
        private static bool TryGetIntoVariable(in TableUnionOperator union, out VariableReference into)
        {
            if (union.Expression1 is SelectExpression select)
            {
                return TryGetIntoVariable(in select, out into);
            }

            into = null;
            return false;
        }
        internal static bool TryGetIntoVariable(in UpdateStatement update, out VariableReference into)
        {
            into = null;

            if (update.Output?.Into?.Value is VariableReference variable)
            {
                into = variable;
            }

            return into is not null;
        }
        internal static IProcessor CreateMessageBrokerProcessor(in StreamScope scope)
        {
            if (scope.Owner is ConsumeStatement consume)
            {
                if (consume.Target.StartsWith("amqp"))
                {
                    return new RabbitMQ.Consumer(in scope);
                }
                else if (consume.Target.StartsWith("kafka"))
                {
                    //TODO: Kafka consumer
                }
            }
            else if (scope.Owner is ProduceStatement produce)
            {
                if (produce.Target.StartsWith("amqp"))
                {
                    return new RabbitMQ.Producer(in scope);
                }
                else if (produce.Target.StartsWith("kafka"))
                {
                    //TODO: Kafka producer
                }
            }

            throw new InvalidOperationException("Unsupported message broker");
        }

        // ***

        internal static void InitializeVariables(in StreamScope scope)
        {
            InitializeVariables(in scope, null);
        }
        internal static void InitializeVariables(in StreamScope scope, in IMetadataProvider database)
        {
            if (scope.Variables.Count == 0) { return; }

            ScriptModel script = new();

            foreach (DeclareStatement declare in scope.Declarations)
            {
                script.Statements.Add(declare); // local scope variable

                if (declare.Initializer is SelectExpression select)
                {
                    List<VariableReference> references = new VariableReferenceExtractor().Extract(select);

                    foreach (VariableReference reference in references)
                    {
                        if (scope.TryGetDeclaration(reference.Identifier, out bool local, out DeclareStatement statement))
                        {
                            if (!local) // outer scope variable
                            {
                                if (statement.Initializer is SelectExpression)
                                {
                                    statement = new DeclareStatement()
                                    {
                                        Name = statement.Name,
                                        Type = statement.Type,
                                        Token = statement.Token
                                    };

                                    if (statement.Type.Binding is Entity entity)
                                    {
                                        statement.Type = new TypeIdentifier()
                                        {
                                            Token = statement.Type.Token,
                                            Binding = statement.Type.Binding,
                                            Identifier = ParserHelper.GetDataTypeLiteral(typeof(Entity))
                                        };

                                        if (scope.TryGetValue(reference.Identifier, out object value) && value is not null)
                                        {
                                            statement.Initializer = new ScalarExpression()
                                            {
                                                Token = TokenType.Entity,
                                                Literal = value.ToString()
                                            };
                                        }
                                        else
                                        {
                                            statement.Initializer = new ScalarExpression()
                                            {
                                                Token = TokenType.Entity,
                                                Literal = entity.ToString()
                                            };
                                        }
                                    }
                                }

                                script.Statements.Insert(0, statement);
                            }
                        }
                    }

                    List<MemberAccessExpression> members = new MemberAccessExtractor().Extract(select);

                    Dictionary<string, DeclareStatement> memberAccess = new();

                    foreach (MemberAccessExpression member in members)
                    {
                        string target = member.GetTargetName();

                        if (!memberAccess.ContainsKey(target))
                        {
                            if (scope.TryGetDeclaration(target, out bool local, out DeclareStatement statement))
                            {
                                if (!local) // outer scope variable
                                {
                                    script.Statements.Insert(0, statement);
                                }
                            }
                        }
                    }
                }
            }

            foreach (StreamScope child in scope.Children)
            {
                if (child.Owner is ImportStatement import)
                {
                    script.Statements.Add(import);
                }
            }

            if (!ScriptProcessor.TryBind(in script, in database, out string error))
            {
                throw new InvalidOperationException(error);
            }

            foreach (DeclareStatement declare in scope.Declarations)
            {
                if (declare.Initializer is null)
                {
                    scope.Variables[declare.Name] = GetDefaultValue(in declare);
                }
                else if (declare.Initializer is ScalarExpression scalar)
                {
                    scope.Variables[declare.Name] = ParserHelper.GetScalarValue(in scalar);
                }
                else if (declare.Initializer is SelectExpression select)
                {
                    scope.Variables[declare.Name] = GetSelectValue(in scope, in database, in declare, in select);
                }
            }

            //TODO: IMPORT statement ?
            //ScriptProcessor.ExecuteImportStatements(in script, in database, in sql_parameters);
        }
        private static object GetDefaultValue(in DeclareStatement declare)
        {
            object value = null;

            if (declare.Type.Binding is Entity entity)
            {
                value = entity;

                string literal = ParserHelper.GetDataTypeLiteral(typeof(Entity));

                if (declare.Type.Identifier != literal) // DECLARE @Ссылка Справочник.Номенклатура
                {
                    declare.Type.Identifier = literal; // DECLARE @Ссылка entity = {code:uuid}

                    declare.Initializer = new ScalarExpression()
                    {
                        Token = TokenType.Entity,
                        Literal = entity.ToString()
                    };
                }
            }
            else if (declare.Type.Binding is Type type)
            {
                value = UnionType.GetDefaultValue(in type);
            }

            return value;
        }
        private static object GetSelectValue(in StreamScope scope, in IMetadataProvider database, in DeclareStatement declare, in SelectExpression select)
        {
            SqlStatement statement = TranspileSelectStatement(in database, in select);

            Dictionary<string, object> select_parameters = new();

            List<VariableReference> references = new VariableReferenceExtractor().Extract(select);

            foreach (VariableReference reference in references)
            {
                if (scope.TryGetValue(reference.Identifier, out object value))
                {
                    select_parameters.Add(reference.Identifier, value);
                }
            }

            List<MemberAccessExpression> members = new MemberAccessExtractor().Extract(select);

            foreach (MemberAccessExpression member in members)
            {
                string target = member.GetTargetName();

                if (!select_parameters.ContainsKey(target))
                {
                    if (scope.TryGetValue(target, out object value))
                    {
                        select_parameters.Add(member.GetDbParameterName(), value);
                    }
                }
            }

            database.ConfigureDbParameters(in select_parameters);

            if (declare.Type.Binding is Entity empty)
            {
                Entity entity = SelectEntityValue(in database, in statement, in select_parameters);

                return entity.IsUndefined ? empty : entity;
            }
            else
            {
                return SelectParameterValue(in database, in statement, in select_parameters);
            }
        }
        private static SqlStatement TranspileSelectStatement(in IMetadataProvider database, in SelectExpression select)
        {
            ScriptModel script = new();

            script.Statements.Add(new SelectStatement()
            {
                Expression = select
            });

            ISqlTranspiler transpiler;

            if (database.DatabaseProvider == DatabaseProvider.SqlServer)
            {
                transpiler = new MsSqlTranspiler() { YearOffset = database.YearOffset };
            }
            else if (database.DatabaseProvider == DatabaseProvider.PostgreSql)
            {
                transpiler = new PgSqlTranspiler() { YearOffset = database.YearOffset };
            }
            else
            {
                throw new InvalidOperationException($"Unsupported database provider: {database.DatabaseProvider}");
            }

            if (!transpiler.TryTranspile(in script, in database, out TranspilerResult result, out string error))
            {
                throw new Exception(error);
            }

            if (result is not null && result.Statements is not null && result.Statements.Count > 0)
            {
                return result.Statements[0];
            }

            throw new InvalidOperationException("Entity parameters configuration error");
        }
        private static Entity SelectEntityValue(in IMetadataProvider context, in SqlStatement statement, in Dictionary<string, object> parameters)
        {
            object value = null;

            IQueryExecutor executor = context.CreateQueryExecutor(); //TODO: use OneDbConnection ?

            foreach (IDataReader reader in executor.ExecuteReader(statement.Script, 10, parameters))
            {
                value = statement.Mapper.Properties[0].GetValue(in reader); break;
            }

            if (value is Entity entity)
            {
                return entity;
            }

            return Entity.Undefined;
        }
        private static object SelectParameterValue(in IMetadataProvider context, in SqlStatement statement, in Dictionary<string, object> parameters)
        {
            object value = null;

            IQueryExecutor executor = context.CreateQueryExecutor(); //TODO: use OneDbConnection ?

            foreach (IDataReader reader in executor.ExecuteReader(statement.Script, 10, parameters))
            {
                value = statement.Mapper.Properties[0].GetValue(in reader); break;
            }

            return value;
        }

        // ***

        internal static ScriptModel CreateProcessorScript(in StreamScope scope)
        {
            ScriptModel script = new();

            // outer scope variables
            script.Statements.AddRange(GetOuterScopeDeclarations(in scope));
            
            // local scope variables
            script.Statements.AddRange(scope.Declarations);

            // processor statement
            script.Statements.Add(scope.Owner);

            return script;
        }
        internal static List<DeclareStatement> GetOuterScopeDeclarations(in StreamScope scope)
        {
            List<DeclareStatement> declarations = new();

            // { boolean, number, datetime, string, binary, uuid, entity, array, object }
            declarations.AddRange(GetOuterScopeVariables(in scope));

            // { @object.member }
            declarations.AddRange(GetOuterScopeMemberAccess(in scope));

            return declarations;
        }
        internal static DeclareStatement[] GetOuterScopeVariables(in StreamScope scope)
        {
            List<VariableReference> references = new VariableReferenceExtractor().Extract(scope.Owner);

            if (references is null || references.Count == 0)
            {
                return Array.Empty<DeclareStatement>();
            }

            List<DeclareStatement> declarations = new(references.Count);

            foreach (VariableReference reference in references)
            {
                if (scope.TryGetDeclaration(reference.Identifier, out bool local, out DeclareStatement statement))
                {
                    if (!local) // outer scope variable
                    {
                        if (statement.Initializer is SelectExpression)
                        {
                            statement = new DeclareStatement()
                            {
                                Name = statement.Name,
                                Type = statement.Type,
                                Token = statement.Token
                            };

                            if (statement.Type.Binding is Entity entity)
                            {
                                statement.Type = new TypeIdentifier()
                                {
                                    Token = statement.Type.Token,
                                    Binding = statement.Type.Binding,
                                    Identifier = ParserHelper.GetDataTypeLiteral(typeof(Entity))
                                };

                                if (scope.TryGetValue(reference.Identifier, out object value) && value is not null)
                                {
                                    statement.Initializer = new ScalarExpression()
                                    {
                                        Token = TokenType.Entity,
                                        Literal = value.ToString()
                                    };
                                }
                                else
                                {
                                    statement.Initializer = new ScalarExpression()
                                    {
                                        Token = TokenType.Entity,
                                        Literal = entity.ToString()
                                    };
                                }
                            }
                        }

                        declarations.Insert(0, statement);
                    }
                }
            }

            return declarations.ToArray();
        }
        internal static DeclareStatement[] GetOuterScopeMemberAccess(in StreamScope scope)
        {
            List<MemberAccessExpression> members = new MemberAccessExtractor().Extract(scope.Owner);

            if (members is null || members.Count == 0)
            {
                return Array.Empty<DeclareStatement>();
            }

            List<DeclareStatement> declarations = new(members.Count);

            Dictionary<string, DeclareStatement> memberAccess = new(members.Count);

            foreach (MemberAccessExpression member in members)
            {
                string target = member.GetTargetName();

                if (!memberAccess.ContainsKey(target))
                {
                    if (scope.TryGetDeclaration(in target, out bool local, out DeclareStatement statement))
                    {
                        if (!local) // outer scope variable
                        {
                            declarations.Insert(0, statement);
                        }
                    }
                }
            }

            return declarations.ToArray();
        }

        internal static SqlStatement Transpile(in StreamScope scope)
        {
            Uri uri = scope.GetDatabaseUri();

            IMetadataProvider database = MetadataService.CreateOneDbMetadataProvider(in uri);

            ScriptModel script = CreateProcessorScript(in scope);

            if (!ScriptProcessor.TryBind(in script, in database, out string error))
            {
                throw new InvalidOperationException(error);
            }

            return TranspileProcessorScript(in script, in database);
        }
        internal static SqlStatement TranspileProcessorScript(in ScriptModel script, in IMetadataProvider database)
        {
            ISqlTranspiler transpiler;

            if (database.DatabaseProvider == DatabaseProvider.SqlServer)
            {
                transpiler = new MsSqlTranspiler() { YearOffset = database.YearOffset };
            }
            else if (database.DatabaseProvider == DatabaseProvider.PostgreSql)
            {
                transpiler = new PgSqlTranspiler() { YearOffset = database.YearOffset };
            }
            else
            {
                throw new InvalidOperationException($"Unsupported database provider: {database.DatabaseProvider}");
            }

            if (!transpiler.TryTranspile(in script, in database, out TranspilerResult result, out string error))
            {
                throw new Exception(error);
            }

            if (result is not null && result.Statements is not null && result.Statements.Count > 0)
            {
                // find SQL command statement
                // THINK: && s.Mapper.Properties.Count > 0
                return result.Statements.Where(s => !string.IsNullOrEmpty(s.Script)).FirstOrDefault();
            }

            throw new InvalidOperationException("Transpilation error");
        }

        internal static void ConfigureVariablesMap(in StreamScope scope, in Dictionary<string, string> map)
        {
            SyntaxNode node = scope.Owner;

            List<VariableReference> variables = new VariableReferenceExtractor().Extract(in node);

            foreach (VariableReference variable in variables) // @variable
            {
                if (variable.Binding is Type type || variable.Binding is Entity entity)
                {
                    // boolean, number, datetime, string, binary, uuid, entity

                    if (scope.TryGetDeclaration(variable.Identifier, out _, out _))
                    {
                        if (!map.ContainsKey(variable.Identifier))
                        {
                            map.Add(variable.Identifier, variable.Identifier);
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"Declaration of [{variable.Identifier}] is not found");
                    }
                }
            }

            List<MemberAccessExpression> members = new MemberAccessExtractor().Extract(in node);

            foreach (MemberAccessExpression member in members) // @object.member
            {
                string target = member.GetTargetName();

                if (scope.TryGetDeclaration(in target, out _, out _))
                {
                    string parameter = member.GetDbParameterName();

                    if (!map.ContainsKey(member.Identifier))
                    {
                        map.Add(member.Identifier, parameter);
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Declaration of [{target}] is not found");
                }
            }
        }
        internal static void ConfigureFunctionsMap(in StreamScope scope, in Dictionary<string, string> map)
        {
            SyntaxNode node = scope.Owner;

            List<FunctionExpression> functions = new DaJetFunctionExtractor().Extract(in node);

            foreach (FunctionExpression function in functions)
            {
                if (function.Name != "DaJet.Json")
                {
                    throw new InvalidOperationException($"Unknown function name: [{function.Name}]");
                }

                if (function.Parameters.Count == 0 ||
                    function.Parameters[0] is not VariableReference variable)
                {
                    throw new InvalidOperationException($"Invalid parameter type: [{function.Name}]");
                }

                if (scope.TryGetDeclaration(variable.Identifier, out _, out DeclareStatement declare))
                {
                    if (declare.Type.Token == TokenType.Object)
                    {
                        if (!map.ContainsKey(variable.Identifier))
                        {
                            map.Add(variable.Identifier, function.GetVariableIdentifier());
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"Invalid parameter type: [{function.Name}]");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Declaration of [{variable.Identifier}] is not found");
                }
            }
        }
    }
}