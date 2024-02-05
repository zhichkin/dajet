using DaJet.Data;
using DaJet.Metadata;
using DaJet.Model;
using DaJet.Scripting;
using DaJet.Scripting.Model;

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
            InfoBaseRecord database = new()
            {
                ConnectionString = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata-ms;Integrated Security=True;Encrypt=False;"
            };

            IMetadataProvider context = MetadataService.CreateOneDbMetadataProvider(in database);

            Dictionary<string, object> parameters = new();

            if (!ScriptProcessor.TryProcess(in context, in script, in parameters, out TranspilerResult result, out string error))
            {
                throw new Exception(error);
            }

            Stream(in context, in result);
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
                    processor = new Streamer(in pipeline, in statement);
                }
                else
                {
                    processor = new Processor(in pipeline, in statement);
                }

                pipeline.Processors.Add(processor);

                if (type == StatementType.Streaming)
                {
                    string objectName = processor.ObjectName;

                    foreach (var append in new AppendOperatorExtractor().Extract(statement.Node))
                    {
                        pipeline.Processors.Add(ProcessorFactory.Create(in pipeline, in append, in objectName));
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