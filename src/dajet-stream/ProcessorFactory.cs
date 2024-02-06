using DaJet.Data;
using DaJet.Scripting;
using DaJet.Scripting.Model;

namespace DaJet.Stream
{
    internal static class ProcessorFactory
    {
        internal static IProcessor Create(in Pipeline pipeline, in TableJoinOperator append, in string objectName)
        {
            if (append is null) { return null; }

            if (append.Token == TokenType.APPEND &&
                append.Expression2 is TableExpression subquery &&
                subquery.Expression is SelectExpression select &&
                select.Modifier is MemberAccessDescriptor descriptor)
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

                if (pipeline.Context.DatabaseProvider == DatabaseProvider.SqlServer)
                {
                    transpiler = new MsSqlTranspiler() { YearOffset = pipeline.Context.YearOffset };
                }
                else if (pipeline.Context.DatabaseProvider == DatabaseProvider.PostgreSql)
                {
                    transpiler = new PgSqlTranspiler() { YearOffset = pipeline.Context.YearOffset };
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported database provider: {pipeline.Context.DatabaseProvider}");
                }

                if (!transpiler.TryTranspile(in script, pipeline.Context, out TranspilerResult result, out string error))
                {
                    throw new InvalidOperationException(error);
                }

                if (result.Statements.Count > 0)
                {
                    SqlStatement statement = result.Statements[0];

                    return new Processor(in pipeline, in statement);
                }
            }

            return null;
        }
    }
}