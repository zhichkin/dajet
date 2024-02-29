using DaJet.Data;
using DaJet.Metadata;
using DaJet.Scripting;
using DaJet.Scripting.Model;

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
            ScriptScope.BuildStreamScope(in script, out ScriptScope scope);

            return new DataStream(in scope);
        }
        internal static IProcessor CreateStream(in ScriptScope parent)
        {
            ScriptScope next;
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
        internal static IProcessor CreateProcessor(in ScriptScope scope)
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

            }
            else if (scope.Owner is ProduceStatement produce)
            {

            }
            else if (scope.Owner is SelectStatement select && select.IsStream)
            {

            }
            else if (scope.Owner is UpdateStatement update && update.Output?.Into?.Value is not null)
            {

            }
            else
            {
                return new DbProcessor(in scope);
            }

            return null; //TODO: error ?
        }
    }
}