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
        public static void Process(in string url)
        {
            IMetadataProvider context = GetDatabaseContext(in url, out string script);

            Dictionary<string, object> parameters = new();

            if (!ScriptProcessor.TryProcess(in context, in script, in parameters, out TranspilerResult result, out string error))
            {
                throw new Exception(error);
            }

            Stream(in context, in result);
        }
        private static IMetadataProvider GetDatabaseContext(in string url, out string script)
        {
            Uri uri = new(url);

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
                
                StatementType type = GetStatementType(in statement);

                if (type == StatementType.Streaming && !stream_starter_is_found)
                {
                    stream_starter_is_found = true;
                    pipeline.Processors.Add(new Streamer(in pipeline, in statement));
                }
                else
                {
                    pipeline.Processors.Add(new Processor(in pipeline, in statement));
                }
            }

            pipeline.Execute();
        }
        private static StatementType GetStatementType(in SqlStatement statement)
        {
            if (statement.Node is SelectStatement node)
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
    }
}