using DaJet.Data;
using DaJet.Metadata;
using DaJet.Model;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using Microsoft.Data.SqlClient;
using Npgsql;
using System.Text;
using System.Web;

namespace DaJet.Stream
{
    public static class StreamProcessor
    {
        private static readonly DaJetDataSource dajet = new();
        public static void Process(in Uri uri)
        {
            IMetadataProvider context = GetDatabaseContext(in uri, out string script);

            Dictionary<string, object> parameters = new();

            if (!ScriptProcessor.TryProcess(in context, in script, in parameters, out TranspilerResult result, out string error))
            {
                throw new Exception(error);
            }

            Stream(in context, in result);
        }
        public static void Process(in string script)
        {
            List<IProcessor> stream = AssemblePipeline(in script);

            stream[0].Process();

            //InfoBaseRecord database = new()
            //{
            //    ConnectionString = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata-ms;Integrated Security=True;Encrypt=False;"
            //};

            //IMetadataProvider context = MetadataService.CreateOneDbMetadataProvider(in database);

            //Dictionary<string, object> parameters = new();

            //if (!ScriptProcessor.TryProcess(in context, in script, in parameters, out TranspilerResult result, out string error))
            //{
            //    throw new Exception(error);
            //}

            //Stream(in context, in result);
        }
        private static List<IProcessor> AssemblePipeline(in string script)
        {
            ScriptModel model;

            using (ScriptParser parser = new())
            {
                if (!parser.TryParse(in script, out model, out string error))
                {
                    //return false;
                }
            }

            List<IProcessor> _processors = new();

            ScriptModel _script = null;
            List<ScriptModel> _scripts = new();
            List<DeclareStatement> _variables = new();
            Dictionary<string, object> _parameters = new();

            for (int i = 0; i < model.Statements.Count; i++)
            {
                SyntaxNode statement = model.Statements[i];

                if (statement is CommentStatement)
                {
                    continue;
                }
                if (statement is DeclareStatement declare)
                {
                    _variables.Add(declare);
                }
                else if (statement is UseStatement use)
                {
                    _script = new ScriptModel();
                    _scripts.Add(_script);
                }
                
                _script.Statements.Add(statement);
            }

            for (int i = 0; i < _scripts.Count; i++)
            {
                ScriptModel _model = _scripts[i];

                foreach (DeclareStatement variable in _variables)
                {
                    if (_model.Statements
                        .Where(statement => statement == variable)
                        .FirstOrDefault() is null)
                    {
                        _model.Statements.Insert(1, variable);
                    }
                }

                UseStatement use = _model.Statements
                    .Where(s => s is UseStatement)
                    .FirstOrDefault() as UseStatement;

                IMetadataProvider _context = GetDatabaseContext(in use);

                if (!new MetadataBinder().TryBind(_model, in _context, out _, out List<string> errors))
                {
                    //error = FormatErrorMessage(in errors); return false;
                }

                if (!new ScriptTransformer().TryTransform(_model, out string error))
                {
                    //return false;
                }

                ISqlTranspiler transpiler = null;

                if (_context.DatabaseProvider == DatabaseProvider.SqlServer)
                {
                    transpiler = new MsSqlTranspiler() { YearOffset = _context.YearOffset };
                }
                else if (_context.DatabaseProvider == DatabaseProvider.PostgreSql)
                {
                    transpiler = new PgSqlTranspiler() { YearOffset = _context.YearOffset };
                }
                else
                {
                    error = $"Unsupported database provider: {_context.DatabaseProvider}";
                }

                if (!transpiler.TryTranspile(in _model, in _context, out TranspilerResult _result, out error))
                {
                    //return false;
                }

                ScriptProcessor.ConfigureParameters(in _model, in _context, _result.Parameters);

                if (i == 0)
                {
                    ScriptProcessor.ConfigureSelectParameters(in _model, in _context, _result.Parameters);
                }

                _processors.AddRange(CreatePipeline(in _context, in _result, in _parameters));
            }

            for (int i = 0; i < _processors.Count - 1; i++)
            {
                _processors[i].LinkTo(_processors[i + 1]);
            }

            return _processors;
        }
        private static IMetadataProvider GetDatabaseContext(in UseStatement use)
        {
            string[] userpass = use.Uri.UserInfo.Split(':');

            string connectionString = string.Empty;

            if (use.Uri.Scheme == "mssql")
            {
                var ms = new SqlConnectionStringBuilder()
                {
                    Encrypt = false,
                    DataSource = use.Uri.Host,
                    InitialCatalog = use.Uri.AbsolutePath.Remove(0, 1)
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
            else if (use.Uri.Scheme == "pgsql")
            {
                var pg = new NpgsqlConnectionStringBuilder()
                {
                    Host = use.Uri.Host,
                    Port = use.Uri.Port,
                    Database = use.Uri.AbsolutePath.Remove(0, 1)
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
        private static List<IProcessor> CreatePipeline(in IMetadataProvider context,
            in TranspilerResult script, in Dictionary<string,object> parameters)
        {
            List<IProcessor> processors = new();
            bool stream_starter_is_found = false;

            foreach (var item in script.Parameters)
            {
                _ = parameters.TryAdd(item.Key, item.Value);
            }

            for (int i = 0; i < script.Statements.Count; i++)
            {
                SqlStatement statement = script.Statements[i];

                if (string.IsNullOrEmpty(statement.Script))
                {
                    continue; //NOTE: USE and DECLARE
                }

                //TODO: use ProcessorFactory for all types of statements

                ProcessorBase processor;

                StatementType type = GetStatementType(in statement);

                if (type == StatementType.Streaming && !stream_starter_is_found)
                {
                    stream_starter_is_found = true;
                    processor = new Streamer(in context, in statement, in parameters);
                }
                else
                {
                    processor = new Processor(in context, in statement, in parameters);
                }

                processors.Add(processor);

                if (type == StatementType.Streaming)
                {
                    string objectName = processor.ObjectName;

                    foreach (var append in new AppendOperatorExtractor().Extract(statement.Node))
                    {
                        processors.Add(ProcessorFactory.Create(in context, in parameters, in append, in objectName));
                    }
                }
            }

            return processors;
        }

        private static IMetadataProvider GetDatabaseContext(in Uri uri, out string script)
        {
            if (uri.Scheme != "dajet")
            {
                throw new InvalidOperationException($"Unknown data source scheme: {uri.Scheme}");
            }

            string host = uri.Host;
            string path = uri.AbsolutePath;
            string scriptPath = host + "/" + path;

            InfoBaseRecord database = dajet.Select<InfoBaseRecord>(host) ?? throw new ArgumentException($"Target not found: {host}");
            ScriptRecord record = dajet.Select<ScriptRecord>(scriptPath) ?? throw new ArgumentException($"Script not found: {path}");

            script = record.Script;

            return MetadataService.CreateOneDbMetadataProvider(in database);
        }
        private static void Stream(in IMetadataProvider context, in TranspilerResult script)
        {
            Pipeline pipeline = new(in context);
            bool stream_starter_is_found = false;

            foreach (var item in script.Parameters)
            {
                pipeline.Parameters.Add(item.Key, item.Value);
            }

            for (int i = 0; i < script.Statements.Count; i++)
            {
                SqlStatement statement = script.Statements[i];

                if (string.IsNullOrEmpty(statement.Script))
                {
                    continue; //NOTE: declaration of parameters
                }

                //TODO: use ProcessorFactory for all types of statements

                ProcessorBase processor;

                StatementType type = GetStatementType(in statement);

                if (type == StatementType.Streaming && !stream_starter_is_found)
                {
                    stream_starter_is_found = true;
                    processor = new Streamer(pipeline.Context, in statement, pipeline.Parameters);
                }
                else
                {
                    processor = new Processor(pipeline.Context, in statement, pipeline.Parameters);
                }

                pipeline.Processors.Add(processor);

                if (type == StatementType.Streaming)
                {
                    string objectName = processor.ObjectName;

                    foreach (var append in new AppendOperatorExtractor().Extract(statement.Node))
                    {
                        pipeline.Processors.Add(ProcessorFactory.Create(pipeline.Context, pipeline.Parameters, in append, in objectName));
                    }
                }
            }

            //TODO: implement Consumer and Updater (stream while table is not empty)

            pipeline.Execute();
        }
        private static StatementType GetStatementType(in SqlStatement statement)
        {
            if (statement.Node is ConsumeStatement)
            {
                return StatementType.Streaming;
            }
            else if (statement.Node is SelectStatement node)
            {
                if (node.Expression is SelectExpression select)
                {
                    return GetStatementType(in select);
                }
                else if (node.Expression is TableUnionOperator union)
                {
                    return GetStatementType(in union);
                }
            }
            else if (statement.Node is UpdateStatement update)
            {
                return GetStatementType(in update);
            }

            return StatementType.Processor;
        }
        private static StatementType GetStatementType(in SelectExpression node)
        {
            if (node.Into is not null &&
                node.Into.Value is VariableReference variable &&
                variable.Binding is TypeIdentifier type)
            {
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
        private static StatementType GetStatementType(in TableUnionOperator node)
        {
            if (node.Expression1 is SelectExpression select)
            {
                return GetStatementType(in select);
            }
            return StatementType.Processor;
        }
        private static StatementType GetStatementType(in UpdateStatement node)
        {
            if (node.Output is not null &&
                node.Output.Into is not null &&
                node.Output.Into.Value is VariableReference variable &&
                variable.Binding is TypeIdentifier type)
            {
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
    }
}