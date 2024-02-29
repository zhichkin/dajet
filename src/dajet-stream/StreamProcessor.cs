using DaJet.Data;
using DaJet.Metadata;
using DaJet.Model;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using Microsoft.Data.SqlClient;
using Npgsql;
using System.Data;
using System.Diagnostics;
using System.Text;
using System.Web;

namespace DaJet.Stream
{
    public static class StreamProcessor
    {
        public static void Process(in string script)
        {
            Stopwatch watch = new();

            watch.Start();

            IProcessor stream = CreateStream(in script);

            watch.Stop();

            long elapsed = watch.ElapsedMilliseconds;

            Console.WriteLine($"Pipeline assembled in {elapsed} ms");

            watch.Restart();

            stream.Process();

            watch.Stop();

            elapsed = watch.ElapsedMilliseconds;

            Console.WriteLine($"Pipeline executed in {elapsed} ms");
        }
        private static string FormatErrorMessage(in List<string> errors)
        {
            if (errors is null || errors.Count == 0)
            {
                return "Unknown binding error";
            }

            StringBuilder error = new();

            for (int i = 0; i < errors.Count; i++)
            {
                if (i > 0) { error.AppendLine(); }

                error.Append(errors[i]);
            }

            return error.ToString();
        }
        private static IProcessor CreateStream(in string script)
        {
            ScriptModel model;

            using (ScriptParser parser = new())
            {
                if (!parser.TryParse(in script, out model, out string error))
                {
                    Console.WriteLine(error);
                }
            }

            return StreamFactory.Create(in model);

            #region "DEPRECATED CODE"

            //List<IProcessor> _processors = new();

            //ScriptModel _script = null;
            //List<ScriptModel> _scripts = new();
            //List<DeclareStatement> _variables = new();
            //Dictionary<string, object> _parameters = new();

            //for (int i = 0; i < model.Statements.Count; i++)
            //{
            //    SyntaxNode statement = model.Statements[i];

            //    if (statement is CommentStatement) { continue; }

            //    if (statement is DeclareStatement declare)
            //    {
            //        _variables.Add(declare);
            //        _script.Statements.Add(statement);
            //    }
            //    else if (statement is UseStatement use)
            //    {
            //        _script = new ScriptModel(); // database context
            //        _scripts.Add(_script);
            //        _script.Statements.Add(statement);
            //    }
            //    else if (statement is ForEachStatement for_each)
            //    {
            //        _script.Statements.Add(statement);

            //        UseStatement _use = _script.Statements
            //            .Where(s => s is UseStatement)
            //            .FirstOrDefault() as UseStatement;

            //        _script = new ScriptModel(); // separate parallelized pipeline
            //        _scripts.Add(_script);

            //        if (_use is not null)
            //        {
            //            _script.Statements.Add(_use);
            //        }
            //    }
            //    else if (statement is ConsumeStatement consume && !string.IsNullOrEmpty(consume.Target))
            //    {
            //        _script = new ScriptModel(); // non-database stream processor
            //        _scripts.Add(_script);
            //        _script.Statements.Add(statement);
            //    }
            //    else if (statement is ProduceStatement produce)
            //    {
            //        _script = new ScriptModel(); // non-database stream processor
            //        _scripts.Add(_script);
            //        _script.Statements.Add(statement);
            //    }
            //    else
            //    {
            //        _script.Statements.Add(statement);
            //    }
            //}

            //PipelineBuilder pipeline = new();
            //Parallelizer parallelizer = null;

            //for (int i = 0; i < _scripts.Count; i++)
            //{
            //    ScriptModel _model = _scripts[i];

            //    int insert = 1;

            //    foreach (DeclareStatement variable in _variables)
            //    {
            //        if (_model.Statements
            //            .Where(statement => statement == variable)
            //            .FirstOrDefault() is null)
            //        {
            //            _model.Statements.Insert(insert++, variable);
            //        }
            //    }

            //    UseStatement use = _model.Statements
            //        .Where(s => s is UseStatement)
            //        .FirstOrDefault() as UseStatement;

            //    if (use is null) // non-database stream processor
            //    {
            //        if (parallelizer is null)
            //        {
            //            //TODO: _processors.AddRange(CreatePipeline(in _context, in _result, in _parameters));
            //        }
            //        else
            //        {
            //            //TODO: pipeline.Add(in _context, in _result);
            //        }
            //    }

            //    IMetadataProvider _context = GetDatabaseContext(in use);

            //    if (!new MetadataBinder().TryBind(_model, in _context, out _, out List<string> errors))
            //    {
            //        Console.WriteLine(FormatErrorMessage(in errors));
            //    }

            //    if (!new ScriptTransformer().TryTransform(_model, out string error))
            //    {
            //        Console.WriteLine(error);
            //    }

            //    ISqlTranspiler transpiler = null;

            //    if (_context.DatabaseProvider == DatabaseProvider.SqlServer)
            //    {
            //        transpiler = new MsSqlTranspiler() { YearOffset = _context.YearOffset };
            //    }
            //    else if (_context.DatabaseProvider == DatabaseProvider.PostgreSql)
            //    {
            //        transpiler = new PgSqlTranspiler() { YearOffset = _context.YearOffset };
            //    }
            //    else
            //    {
            //        error = $"Unsupported database provider: {_context.DatabaseProvider}";
            //    }

            //    if (!transpiler.TryTranspile(in _model, in _context, out TranspilerResult _result, out error))
            //    {
            //        Console.WriteLine(error);
            //    }

            //    ScriptProcessor.ConfigureParameters(in _model, in _context, _result.Parameters);

            //    if (i == 0)
            //    {
            //        ScriptProcessor.ConfigureSelectParameters(in _model, in _context, _result.Parameters);
            //    }

            //    if (parallelizer is null)
            //    {
            //        _processors.AddRange(CreatePipeline(in _context, in _result, in _parameters));

            //        parallelizer = _processors.Where(p => p is Parallelizer).FirstOrDefault() as Parallelizer;
            //    }
            //    else
            //    {
            //        pipeline.Add(in _context, in _result);
            //    }
            //}

            //parallelizer?.SetPipelineBuilder(in pipeline);

            //for (int i = 0; i < _processors.Count - 1; i++)
            //{
            //    _processors[i].LinkTo(_processors[i + 1]);
            //}

            //return _processors;

            #endregion
        }
        internal static IMetadataProvider GetDatabaseContext(in Uri uri)
        {
            string[] userpass = uri.UserInfo.Split(':');

            string connectionString = string.Empty;

            if (uri.Scheme == "mssql")
            {
                var ms = new SqlConnectionStringBuilder()
                {
                    Encrypt = false,
                    DataSource = uri.Host,
                    InitialCatalog = uri.AbsolutePath.Remove(0, 1) // slash
                };

                if (userpass is not null && userpass.Length == 2)
                {
                    ms.UserID = HttpUtility.UrlDecode(userpass[0], Encoding.UTF8);
                    ms.Password = HttpUtility.UrlDecode(userpass[1], Encoding.UTF8);
                }
                else
                {
                    ms.IntegratedSecurity = true;
                }

                connectionString = ms.ToString();
            }
            else if (uri.Scheme == "pgsql")
            {
                var pg = new NpgsqlConnectionStringBuilder()
                {
                    Host = uri.Host,
                    Port = uri.Port,
                    Database = uri.AbsolutePath.Remove(0, 1)
                };

                if (userpass is not null && userpass.Length == 2)
                {
                    pg.Username = HttpUtility.UrlDecode(userpass[0], Encoding.UTF8);
                    pg.Password = HttpUtility.UrlDecode(userpass[1], Encoding.UTF8);
                }
                else
                {
                    pg.IntegratedSecurity = true;
                }

                connectionString = pg.ToString();
            }

            InfoBaseRecord database = new()
            {
                ConnectionString = connectionString
            };

            return MetadataService.CreateOneDbMetadataProvider(in database);
        }
        internal static void InitializeVariables(in ScriptScope scope)
        {
            InitializeVariables(in scope, null);
        }
        internal static void InitializeVariables(in ScriptScope scope, in IMetadataProvider database)
        {
            if (scope.Variables.Count == 0) { return; }

            ScriptModel script = new();

            foreach (var variable in scope.Variables)
            {
                if (variable.Value is DeclareStatement declare)
                {
                    script.Statements.Add(declare); // local scope variable

                    if (declare.Initializer is SelectExpression select)
                    {
                        List<VariableReference> references = new VariableReferenceExtractor().Extract(select);
                        
                        foreach (VariableReference reference in references)
                        {
                            if (scope.TryGetVariableDeclaration(reference.Identifier, out bool local, out DeclareStatement statement))
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

                                            if (scope.TryGetVariableValue(reference.Identifier, out object value) && value is not null)
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
                                if (scope.TryGetVariableDeclaration(target, out bool local, out DeclareStatement statement))
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
            }

            foreach (ScriptScope child in scope.Children)
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

            foreach (var variable in scope.Variables)
            {
                if (variable.Value is DeclareStatement declare)
                {
                    if (declare.Initializer is null)
                    {
                        scope.Context[variable.Key] = GetDefaultValue(in declare);
                    }
                    else if (declare.Initializer is ScalarExpression scalar)
                    {
                        scope.Context[variable.Key] = ParserHelper.GetScalarValue(in scalar);
                    }
                    else if (declare.Initializer is SelectExpression select)
                    {
                        scope.Context[variable.Key] = GetSelectValue(in scope, in database, in declare, in select);
                    }
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
        private static object GetSelectValue(in ScriptScope scope, in IMetadataProvider database, in DeclareStatement declare, in SelectExpression select)
        {
            SqlStatement statement = TranspileSelectStatement(in database, in select);
            
            Dictionary<string, object> select_parameters = new();

            List<VariableReference> references = new VariableReferenceExtractor().Extract(select);

            foreach (VariableReference reference in references)
            {
                if (scope.TryGetVariableValue(reference.Identifier, out object value))
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
                    if (scope.TryGetVariableValue(target, out object value))
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