﻿using DaJet.Data;
using DaJet.Metadata;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Data;

namespace DaJet.Stream
{
    internal static class StreamFactory
    {
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
            else if (scope.Owner is RequestStatement)
            {
                return new Http.Request(in scope);
            }
            else if (scope.Owner is ConsumeStatement consume && !string.IsNullOrEmpty(consume.Target))
            {
                return CreateMessageBrokerProcessor(in scope);
            }
            else if (scope.Owner is ProduceStatement)
            {
                return CreateMessageBrokerProcessor(in scope);
            }
            
            return CreateDatabaseProcessor(in scope);
        }
        internal static IProcessor CreateDatabaseProcessor(in StreamScope scope)
        {
            if (TryGetIntoVariable(scope.Owner, out VariableReference variable))
            {
                if (!scope.TryGetDeclaration(variable.Identifier, out _, out DeclareStatement declare))
                {
                    throw new InvalidOperationException($"INTO {variable.Identifier} is not declared");
                }

                if (declare.Type.Token == TokenType.Array)
                {
                    return new IntoArrayProcessor(in scope);
                }
                else if (declare.Type.Token == TokenType.Object)
                {
                    if (scope.Owner is UpdateStatement)
                    {
                        return new StreamProcessor(in scope); //THINK: UPDATE TOP 1000 [ConsumeProcessor]
                    }
                    else if (scope.Owner is ConsumeStatement consume && consume.From is not null)
                    {
                        return new ConsumeProcessor(in scope);
                    }
                    else if (scope.Owner is SelectStatement select) //NOTE: APPEND operators -> AppendProcessor
                    {
                        if (select.IsStream)
                        {
                            return new StreamProcessor(in scope);
                        }
                        else
                        {
                            return new IntoObjectProcessor(in scope);
                        }
                    }
                }
            }
            
            return new NonQueryProcessor(in scope);
        }
        internal static IProcessor CreateAppendStream(in StreamScope parent, in VariableReference target)
        {
            List<TableJoinOperator> appends = new AppendOperatorExtractor().Extract(parent.Owner);

            if (appends is null || appends.Count == 0)
            {
                return null;
            }

            IProcessor starter = null;
            IProcessor current = null;
            IProcessor processor;
            TableJoinOperator next;

            for (int i = 0; i < appends.Count; i++)
            {
                next = appends[i];

                processor = CreateAppendProcessor(in parent, in target, in next);

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
        internal static IProcessor CreateAppendProcessor(in StreamScope parent, in VariableReference target, in TableJoinOperator append)
        {
            if (append.Token == TokenType.APPEND && append.Expression2 is TableExpression subquery)
            {
                SelectStatement statement = new() { Expression = subquery.Expression };

                StreamScope scope = new(statement, parent);

                if (append.Modifier == TokenType.Array)
                {
                    return new AppendArrayProcessor(in scope, in target, subquery.Alias);
                }
                else if (append.Modifier == TokenType.Object)
                {
                    return new AppendObjectProcessor(in scope, in target, subquery.Alias);
                }
            }

            return null; // Что-то пошло не так ¯\_(ツ)_/¯
        }
        internal static bool TryGetIntoVariable(in SyntaxNode node, out VariableReference into)
        {
            into = null;

            if (node is SelectStatement select)
            {
                return TryGetIntoVariable(in select, out into);
            }
            else if (node is ConsumeStatement consume)
            {
                return TryGetIntoVariable(in consume, out into);
            }
            else if (node is UpdateStatement update)
            {
                return TryGetIntoVariable(in update, out into);
            }

            return into is not null;
        }
        internal static bool TryGetIntoVariable(in ConsumeStatement consume, out VariableReference into)
        {
            into = null;

            if (consume.Into?.Value is VariableReference variable)
            {
                into = variable;
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
                    return new Kafka.Consumer(in scope);
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
                    return new Kafka.Producer(in scope);
                }
            }

            throw new InvalidOperationException("Unsupported service");
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
                else if (declare.Initializer is SelectExpression || declare.Initializer is TableUnionOperator)
                {
                    SelectStatement statement = new() { Expression = declare.Initializer };

                    scope.Variables[declare.Name] = GetSelectValue(in scope, in database, in declare, in statement);
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
        private static object GetSelectValue(in StreamScope scope, in IMetadataProvider database, in DeclareStatement declare, in SelectStatement select)
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
            else if (declare.Type.Token == TokenType.Array)
            {
                return SelectArrayValue(in database, in statement, in select_parameters);
            }
            else if (declare.Type.Token == TokenType.Object)
            {
                return SelectObjectValue(in database, in statement, in select_parameters);
            }
            else
            {
                return SelectParameterValue(in database, in statement, in select_parameters);
            }
        }
        private static SqlStatement TranspileSelectStatement(in IMetadataProvider database, in SelectStatement select)
        {
            ScriptModel script = new();

            script.Statements.Add(select);

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

            IQueryExecutor executor = context.CreateQueryExecutor();

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

            IQueryExecutor executor = context.CreateQueryExecutor();

            foreach (IDataReader reader in executor.ExecuteReader(statement.Script, 10, parameters))
            {
                value = statement.Mapper.Properties[0].GetValue(in reader); break;
            }

            return value;
        }
        private static DataObject SelectObjectValue(in IMetadataProvider context, in SqlStatement statement, in Dictionary<string, object> parameters)
        {
            DataObject value = new(statement.Mapper.Properties.Count);

            IQueryExecutor executor = context.CreateQueryExecutor();

            foreach (IDataReader reader in executor.ExecuteReader(statement.Script, 10, parameters))
            {
                statement.Mapper.Map(in reader, in value); break;
            }

            return value;
        }
        private static List<DataObject> SelectArrayValue(in IMetadataProvider context, in SqlStatement statement, in Dictionary<string, object> parameters)
        {
            List<DataObject> table = new();

            IQueryExecutor executor = context.CreateQueryExecutor();

            foreach (IDataReader reader in executor.ExecuteReader(statement.Script, 10, parameters))
            {
                DataObject record = new(statement.Mapper.Properties.Count);

                statement.Mapper.Map(in reader, in record);

                table.Add(record);
            }

            return table;
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

            HashSet<string> deduplicate = new();

            // boolean, number, datetime, string, binary, uuid, entity, array, object
            foreach (DeclareStatement declare in GetOuterScopeVariables(in scope))
            {
                if (!deduplicate.Contains(declare.Name))
                {
                    declarations.Add(declare);
                    deduplicate.Add(declare.Name);
                }
            }

            // @variable.member -> DECLARE @variable object
            foreach (DeclareStatement declare in GetOuterScopeMemberAccess(in scope))
            {
                if (!deduplicate.Contains(declare.Name))
                {
                    declarations.Add(declare);
                    deduplicate.Add(declare.Name);
                }
            }

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
            if (scope.TryGetTranspilation(scope.Owner, out SqlStatement statement))
            {
                return statement;
            }

            if (!scope.TryGetMetadataProvider(out IMetadataProvider database, out string error))
            {
                throw new InvalidOperationException(error);
            }

            ScriptModel script = CreateProcessorScript(in scope);

            if (!ScriptProcessor.TryBind(in script, in database, out error))
            {
                throw new InvalidOperationException(error);
            }

            statement = TranspileProcessorScript(in script, in database);

            if (statement is null)
            {
                throw new InvalidOperationException($"Transpilation error: [{scope.Owner}]");
            }

            StreamScope root = scope.GetRoot(); //NOTE: ScriptModel is the root

            root.Transpilations.Add(scope.Owner, statement);

            return statement;
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
                return result.Statements.Where(s => !string.IsNullOrEmpty(s.Script)).FirstOrDefault();
            }

            return null;
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

        internal static void ConfigureIteratorSchema(in StreamScope scope, out string item, out string iterator)
        {
            if (scope.Owner is not ForEachStatement statement)
            {
                throw new ArgumentException(nameof(ConfigureIteratorSchema));
            }

            item = statement.Variable.Identifier;
            iterator = statement.Iterator.Identifier;

            // dynamic object schema binding - inferring schema from iterator

            if (!scope.TryGetDeclaration(in iterator, out _, out DeclareStatement schema))
            {
                throw new InvalidOperationException($"Declaration of {iterator} is not found");
            }

            if (!scope.TryGetDeclaration(in item, out _, out DeclareStatement declare))
            {
                throw new InvalidOperationException($"Declaration of {item} is not found");
            }

            declare.Type.Binding = schema.Type.Binding;
        }
        internal static List<string> GetClosureVariables(in StreamScope scope)
        {
            if (scope.Owner is not ForEachStatement statement)
            {
                throw new ArgumentException(nameof(GetClosureVariables));
            }

            List<string> closure = new();

            HashSet<string> identifiers = new();

            GetIdentifiersRecursively(in scope, in identifiers);

            _ = identifiers.Remove(statement.Iterator.Identifier);

            foreach (string identifier in identifiers)
            {
                if (scope.TryGetDeclaration(identifier, out _, out _))
                {
                    closure.Add(identifier);
                }
            }

            return closure;
        }
        internal static void GetIdentifiersRecursively(in StreamScope scope, in HashSet<string> identifiers)
        {
            SyntaxNode node = scope.Owner;

            string identifier;

            List<VariableReference> variables = new VariableReferenceExtractor().Extract(in node);

            foreach (VariableReference variable in variables) // @variable
            {
                identifier = variable.Identifier;

                if (!identifiers.Contains(identifier))
                {
                    identifiers.Add(identifier);
                }
            }

            List<MemberAccessExpression> members = new MemberAccessExtractor().Extract(in node);

            foreach (MemberAccessExpression member in members) // @object.member
            {
                identifier = member.GetTargetName();

                if (!identifiers.Contains(identifier))
                {
                    identifiers.Add(identifier);
                }
            }

            foreach (StreamScope child in scope.Children)
            {
                GetIdentifiersRecursively(in child, in identifiers);
            }
        }

        // ***

        internal static void BindVariables(in StreamScope scope)
        {
            ScriptModel script = CreateProcessorScript(in scope);

            if (!ScriptProcessor.TryBind(in script, null, out string error))
            {
                throw new InvalidOperationException(error);
            }
        }
        internal static bool TryGetOption(in StreamScope scope, in string name, out object value)
        {
            if (scope.TryGetValue(name, out value))
            {
                if (value is VariableReference variable)
                {
                    if (scope.TryGetValue(variable.Identifier, out value))
                    {
                        return true;
                    }
                }
                else if (value is MemberAccessExpression member)
                {
                    if (scope.TryGetValue(member.Identifier, out value))
                    {
                        return true;
                    }
                }
                else if (value is FunctionExpression function
                    && function.Name == "DaJet.Json"
                    && function.Parameters.Count > 0
                    && function.Parameters[0] is VariableReference parameter)
                {
                    if (scope.TryGetValue(parameter.Identifier, out value))
                    {
                        if (value is DataObject record)
                        {
                            value = StreamScope.ToJson(in record);
                            
                            return true;
                        }
                    }
                }
                else
                {
                    return true; // scalar value
                }
            }

            value = null;
            return false;
        }
        internal static void MapOptions(in StreamScope scope)
        {
            if (scope.Owner is ProduceStatement produce)
            {
                MapColumnExpressions(in scope, produce.Options);
                MapColumnExpressions(in scope, produce.Columns);
            }
            else if (scope.Owner is ConsumeStatement consume)
            {
                MapColumnExpressions(in scope, consume.Options);
                MapColumnExpressions(in scope, consume.Columns);
            }
        }
        private static void MapColumnExpressions(in StreamScope scope, in List<ColumnExpression> columns)
        {
            foreach (ColumnExpression column in columns)
            {
                if (column.Expression is ScalarExpression scalar)
                {
                    scope.Variables.Add(column.Alias, ParserHelper.GetScalarValue(in scalar));
                }
                else if (column.Expression is VariableReference variable)
                {
                    scope.Variables.Add(column.Alias, variable);
                }
                else if (column.Expression is MemberAccessExpression member)
                {
                    scope.Variables.Add(column.Alias, member);
                }
                else if (column.Expression is FunctionExpression function
                    && function.Name == "DaJet.Json"
                    && function.Parameters.Count > 0
                    && function.Parameters[0] is VariableReference parameter
                    && parameter.Binding is TypeIdentifier)
                {
                    scope.Variables.Add(column.Alias, function);
                }
            }
        }
        internal static bool TryGetValue(in StreamScope scope, in SyntaxNode accessor, out object value)
        {
            if (accessor is ScalarExpression scalar)
            {
                value = ParserHelper.GetScalarValue(in scalar);

                return true;
            }
            else if (accessor is VariableReference variable)
            {
                if (scope.TryGetValue(variable.Identifier, out value))
                {
                    return true;
                }
            }
            else if (accessor is MemberAccessExpression member)
            {
                if (scope.TryGetValue(member.Identifier, out value))
                {
                    return true;
                }
            }
            else if (accessor is FunctionExpression function
                && function.Name == "DaJet.Json"
                && function.Parameters.Count > 0
                && function.Parameters[0] is VariableReference parameter)
            {
                if (scope.TryGetValue(parameter.Identifier, out value))
                {
                    if (value is DataObject record)
                    {
                        value = StreamScope.ToJson(in record);

                        return true;
                    }
                }
            }

            value = null;
            return false;
        }
    }
}