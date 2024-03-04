using DaJet.Data;
using DaJet.Metadata;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Data;

namespace DaJet.Stream
{
    internal static class StreamFactory
    {
        internal static IProcessor Create(
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

                    return new Processor(in context, in statement, in parameters);
                }
            }

            return null;
        }
        
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
            if (scope.Owner is SelectStatement select)
            {
                if (TryGetIntoVariable(in select, out _, out TokenType type))
                {
                    if (type == TokenType.Array)
                    {
                        return new SelectIntoArrayProcessor(in scope);
                    }
                    else if (type == TokenType.Object)
                    {
                        return new SelectIntoObjectProcessor(in scope);
                    }
                }
            }
            else if (scope.Owner is UpdateStatement update)
            {
                if (TryGetIntoVariable(in update, out _, out TokenType type))
                {
                    if (type == TokenType.Array)
                    {
                        return new UpdateIntoArrayProcessor(in scope);
                    }
                    else if (type == TokenType.Object)
                    {
                        return new UpdateIntoObjectProcessor(in scope);
                    }
                }
            }
            
            return new NonQueryProcessor(in scope);
        }
        internal static bool TryGetIntoVariable(in SelectStatement statement, out VariableReference into, out TokenType type)
        {
            if (statement.Expression is SelectExpression select)
            {
                return TryGetIntoVariable(in select, out into, out type);
            }
            else if (statement.Expression is TableUnionOperator union)
            {
                return TryGetIntoVariable(in union, out into, out type);
            }

            into = null;
            type = TokenType.Ignore;
            return false;
        }
        private static bool TryGetIntoVariable(in SelectExpression select, out VariableReference into, out TokenType type)
        {
            if (select.Into is not null &&
                select.Into.Value is VariableReference variable) // TODO: the variable is not bound yet !!!
                //&& variable.Binding is TypeIdentifier identifier)
            {
                into = variable;
                type = TokenType.Object; //identifier.Token; // { Array | Object }
                return true;
            }

            into = null;
            type = TokenType.Ignore;
            return false;
        }
        private static bool TryGetIntoVariable(in TableUnionOperator union, out VariableReference into, out TokenType type)
        {
            if (union.Expression1 is SelectExpression select)
            {
                return TryGetIntoVariable(in select, out into, out type);
            }

            into = null;
            type = TokenType.Ignore;
            return false;
        }
        internal static bool TryGetIntoVariable(in UpdateStatement update, out VariableReference into, out TokenType type)
        {
            if (update.Output is not null &&
                update.Output.Into is not null &&
                update.Output.Into.Value is VariableReference variable &&
                variable.Binding is TypeIdentifier identifier)
            {
                into = variable;
                type = identifier.Token; // { Array | Object }
                return true;
            }

            into = null;
            type = TokenType.Ignore;
            return false;
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

                }
            }

            throw new InvalidOperationException("Unsupported message broker type");
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

            if (!ScriptProcessor.TryPrepareScript(in script, in database, out string error))
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

            if (!ScriptProcessor.TryPrepareScript(in script, in database, out string error))
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

        //internal static List<IProcessor> CreatePipeline(in IMetadataProvider context,
        //    in TranspilerResult script, in Dictionary<string,object> parameters)
        //{
        //    IProcessor processor = null;
        //    List<IProcessor> processors = new();
        //    bool stream_starter_is_found = false;

        //    foreach (var item in script.Parameters)
        //    {
        //        _ = parameters.TryAdd(item.Key, item.Value);
        //    }

        //    for (int i = 0; i < script.Statements.Count; i++)
        //    {
        //        SqlStatement statement = script.Statements[i];

        //        if (statement.Node is ForEachStatement)
        //        {
        //            processor = new Parallelizer(in context, in statement, in parameters);
        //            processors.Add(processor);
        //            continue;
        //        }
        //        else if (statement.Node is ConsumeStatement consume && !string.IsNullOrEmpty(consume.Target))
        //        {
        //            if (consume.Target.StartsWith("amqp"))
        //            {
        //                processors.Add(new RabbitMQ.Consumer(in consume, in parameters));
        //            }
        //            else if (consume.Target.StartsWith("kafka"))
        //            {
        //                //TODO: processors.Add(new Kafka.Consumer(in consume));
        //            }
        //            else
        //            {
        //                throw new InvalidOperationException("Unknown schema provider");
        //            }
        //            continue;
        //        }
        //        else if (statement.Node is ProduceStatement produce)
        //        {
        //            if (produce.Target.StartsWith("amqp"))
        //            {
        //                processors.Add(new RabbitMQ.Producer(in produce, in parameters));
        //            }
        //            else if (produce.Target.StartsWith("kafka"))
        //            {
        //                //TODO: processors.Add(new Kafka.Producer(in produce));
        //            }
        //            else
        //            {
        //                throw new InvalidOperationException("Unknown schema provider");
        //            }
        //            continue;
        //        }

        //        if (string.IsNullOrEmpty(statement.Script))
        //        {
        //            continue; //NOTE: USE, DECLARE, FOR EACH, PRODUCE, CONSUME <uri>
        //        }

        //        //TODO: use ProcessorFactory for all types of statements

        //        StatementType type = GetStatementType(in statement);

        //        if (type == StatementType.Streaming && !stream_starter_is_found)
        //        {
        //            //TODO: check if consume statement is present in upcoming scripts !!!

        //            stream_starter_is_found = true;

        //            if (statement.Node is ConsumeStatement consume && !string.IsNullOrEmpty(consume.Target))
        //            {
        //                if (consume.Target.StartsWith("amqp"))
        //                {
        //                    processor = new RabbitMQ.Consumer(in consume, in parameters);
        //                }
        //                else if (consume.Target.StartsWith("kafka"))
        //                {
        //                    //TODO: processor = new Kafka.Consumer(in consume);
        //                }
        //                else
        //                {
        //                    throw new InvalidOperationException("Unknown schema provider");
        //                }
        //            }
        //            else // database context
        //            {
        //                processor = new Streamer(in context, in statement, in parameters);
        //            }
        //        }
        //        else
        //        {
        //            processor = new Processor(in context, in statement, in parameters);
        //        }

        //        processors.Add(processor);

        //        if (type == StatementType.Streaming)
        //        {
        //            string objectName = string.Empty; //TODO: processor.ObjectName;

        //            foreach (var append in new AppendOperatorExtractor().Extract(statement.Node))
        //            {
        //                processors.Add(PipelineFactory.Create(in context, in parameters, in append, in objectName));
        //            }
        //        }
        //    }

        //    return processors;
        //}
    }
}