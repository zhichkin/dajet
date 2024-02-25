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

        private static void BuildScriptScope(in ScriptModel script, out ScriptScope scope)
        {
            scope = new ScriptScope() { Owner = script };

            ScriptScope _current = scope;

            for (int i = 0; i < script.Statements.Count; i++)
            {
                SyntaxNode statement = script.Statements[i];

                if (statement is CommentStatement) { continue; }

                if (statement is DeclareStatement declare)
                {
                    _current.Variables.Add(declare.Name, declare);
                }
                else if (ScriptScope.IsStreamScope(in statement))
                {
                    if (_current.Owner is UseStatement && statement is UseStatement)
                    {
                        _current = _current.CloseScope(); // one database context closes another
                    }
                    _current = _current.NewScope(in statement); // create parent scope
                }
                else
                {
                    _ = _current.NewScope(in statement); // add child to parent scope
                }
            }
        }
        private static IProcessor Create(in ScriptScope scope)
        {
            if (scope.Owner is ScriptModel)
            {
                return new DataStream(in scope);
            }
            else if (scope.Owner is UseStatement)
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
                //TODO: ???
            }

            return null; //TODO: error ?
        }
        internal static IProcessor Create(in ScriptModel script)
        {
            BuildScriptScope(in script, out ScriptScope scope);

            return Create(in scope);
        }
        internal static IProcessor Create(in List<ScriptScope> children)
        {
            ScriptScope next;
            IProcessor starter = null;
            IProcessor current = null;
            IProcessor processor;

            for (int i = 0; i < children.Count; i++)
            {
                next = children[i];

                processor = Create(in next);

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
    }
}