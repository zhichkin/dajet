using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class PgSqlGenerator : SqlGenerator
    {
        protected override void Visit(in SelectStatement node, in StringBuilder script)
        {
            script.AppendLine();

            if (node.CommonTables is not null)
            {
                script.Append("WITH ");

                if (IsRecursive(node.CommonTables))
                {
                    script.Append("RECURSIVE ");
                }

                Visit(node.CommonTables, in script);
            }

            Visit(node.Select, in script);

            script.Append(';');
        }
        protected override void Visit(in SelectExpression node, in StringBuilder script)
        {
            script.Append("SELECT");

            if (node.Distinct)
            {
                script.Append(" DISTINCT");
            }

            script.AppendLine();

            for (int i = 0; i < node.Select.Count; i++)
            {
                if (i > 0) { script.AppendLine(","); }

                Visit(node.Select[i], in script);
            }

            if (node.From is not null) { Visit(node.From, in script); }
            if (node.Where is not null) { Visit(node.Where, in script); }
            if (node.Group is not null) { Visit(node.Group, in script); }
            if (node.Having is not null) { Visit(node.Having, in script); }
            if (node.Order is not null) { Visit(node.Order, in script); }
            if (node.Top is not null) { Visit(node.Top, in script); }

            if (!string.IsNullOrEmpty(node.Hints))
            {
                script.AppendLine().Append(node.Hints); // TODO: (pg) refactor this hack from CONSUME command
            }
        }
        protected override void Visit(in TopClause node, in StringBuilder script)
        {
            script.AppendLine().Append("LIMIT ");
            Visit(node.Expression, in script);
        }
        private bool IsRecursive(in CommonTableExpression cte)
        {
            if (IsRecursive(in cte, cte.Expression))
            {
                return true;
            }
            
            if (cte.Next is null) { return false; }
            
            return IsRecursive(cte.Next);
        }
        private bool IsRecursive(in CommonTableExpression cte, in SyntaxNode node)
        {
            if (node is SelectExpression select)
            {
                return IsRecursive(in cte, in select);
            }
            else if (node is TableJoinOperator join)
            {
                return IsRecursive(in cte, in join);
            }
            else if (node is TableUnionOperator union)
            {
                return IsRecursive(in cte, in union);
            }
            else if (node is TableReference table)
            {
                return IsRecursive(in cte, in table);
            }

            return false;
        }
        private bool IsRecursive(in CommonTableExpression cte, in SelectExpression select)
        {
            return IsRecursive(in cte, select.From.Expression);
        }
        private bool IsRecursive(in CommonTableExpression cte, in TableJoinOperator node)
        {
            if (IsRecursive(in cte, node.Expression1))
            {
                return true;
            }

            return IsRecursive(in cte, node.Expression2);
        }
        private bool IsRecursive(in CommonTableExpression cte, in TableUnionOperator node)
        {
            if (node.Expression1 is SelectExpression select1)
            {
                if (IsRecursive(in cte, in select1)) { return true; }
            }
            else if (node.Expression1 is TableUnionOperator union1)
            {
                if (IsRecursive(in cte, in union1)) { return true; }
            }

            if (node.Expression2 is SelectExpression select2)
            {
                return IsRecursive(in cte, in select2);
            }
            else if (node.Expression2 is TableUnionOperator union2)
            {
                return IsRecursive(in cte, in union2);
            }

            return false;
        }
        private bool IsRecursive(in CommonTableExpression cte, in TableReference table)
        {
            return (table.Binding == cte);
        }
        protected override void Visit(in EnumValue node, in StringBuilder script)
        {
            script.Append($"CAST(E'\\\\x{ScriptHelper.GetUuidHexLiteral(node.Uuid)}' AS bytea)");
        }
        protected override void Visit(in ScalarExpression node, in StringBuilder script)
        {
            if (node.Token == TokenType.Boolean)
            {
                script.Append(node.Literal);
            }
            else if (node.Token == TokenType.DateTime)
            {
                if (DateTime.TryParse(node.Literal, out DateTime datetime))
                {
                    script.Append($"\'{datetime.AddYears(YearOffset):yyyy-MM-ddTHH:mm:ss}\'::timestamp");
                }
                else
                {
                    script.Append(node.Literal);
                }
            }
            else if (node.Token == TokenType.String)
            {
                script.Append($"CAST(\'{node.Literal}\' AS mvarchar)");
            }
            else if (node.Token == TokenType.Uuid)
            {
                script.Append($"CAST(E'\\\\x{ScriptHelper.GetUuidHexLiteral(new Guid(node.Literal))}' AS bytea)");
            }
            else if (node.Token == TokenType.Binary || node.Literal.StartsWith("0x"))
            {
                if (node.Literal == "0x00") // TODO: подумать как убрать этот костыль
                {
                    script.Append("FALSE");
                }
                else if (node.Literal == "0x01") // TODO: может прилетать как значение по умолчанию для INSERT
                {
                    script.Append("TRUE");
                }
                else
                {
                    script.Append($"CAST(E'\\\\{node.Literal.TrimStart('0')}' AS bytea)");
                }
            }
            else // Number
            {
                script.Append(node.Literal);
            }
        }
        protected override void Visit(in VariableReference node, in StringBuilder script)
        {
            if (node.Binding is Type type && type == typeof(string))
            {
                script.Append($"CAST({node.Identifier} AS mvarchar)");
            }
            else
            {
                script.Append(node.Identifier);
            }
        }
        protected override void Visit(in FunctionExpression node, in StringBuilder script)
        {
            if (node.Name.ToUpperInvariant() == "ISNULL")
            {
                node.Name = "COALESCE";
            }
            if (node.Name.ToUpperInvariant() == "DATALENGTH")
            {
                node.Name = "OCTET_LENGTH";
                script.Append(node.Name).Append('(');
                script.Append("CAST(");
                Visit(node.Parameters[0], in script);
                script.Append(" AS text)");
                script.Append(')');
                return; //TODO: OCTET_LENGTH - what if data type of column is bytea ?
            }

            script.Append(node.Name).Append("(");

            SyntaxNode expression;

            for (int i = 0; i < node.Parameters.Count; i++)
            {
                expression = node.Parameters[i];
                if (i > 0) { script.Append(", "); }

                if (node.Name == "SUBSTRING" && i == 0)
                {
                    script.Append("CAST(");
                    Visit(in expression, in script);
                    script.Append(" AS varchar)");
                }
                else
                {
                    Visit(in expression, in script);
                }
            }

            script.Append(")");

            if (node.Over is not null)
            {
                script.Append(" ");
                Visit(node.Over, in script);
            }
        }
        protected override void Visit(in TableVariableExpression node, in StringBuilder script)
        {
            script.Append($"CREATE TEMPORARY TABLE {node.Name} AS").AppendLine();

            base.Visit(in node, in script);

            script.Append(';').AppendLine();
        }
        protected override void Visit(in TemporaryTableExpression node, in StringBuilder script)
        {
            script.Append($"CREATE TEMPORARY TABLE {node.Name} AS").AppendLine();

            base.Visit(in node, in script);

            script.Append(';').AppendLine();
        }

        protected override void Visit(in DeleteStatement node, in StringBuilder script)
        {
            if (node.Target.Binding is CommonTableExpression)
            {
                throw new InvalidOperationException("DELETE: computed table (cte) targeting is not allowed.");
            }

            if (node.CommonTables is not null)
            {
                script.Append("WITH ");

                Visit(node.CommonTables, in script);
            }

            script.Append("DELETE FROM ");

            VisitTargetTable(node.Target, in script);

            // TODO: add USING clause if another tables are referenced in WHERE clause

            if (node.Where is not null)
            {
                Visit(node.Where, script);
            }

            if (node.Output is not null)
            {
                Visit(node.Output, script);
            }
        }
        protected override void Visit(in OutputClause node, in StringBuilder script)
        {
            script.AppendLine().AppendLine("RETURNING");

            for (int i = 0; i < node.Columns.Count; i++)
            {
                if (i > 0) { script.AppendLine(","); }

                Visit(node.Columns[i], in script);
            }
        }

        protected override void Visit(in ConsumeStatement node, in StringBuilder script)
        {
            ScriptTransformer transformer = new();

            script.AppendLine("queue AS (").Append("DELETE");
            if (node.From is not null) { Visit(node.From, in script); }
            script.AppendLine(" AS source USING filter");

            SelectStatement consume = TransformConsumeToSelect(in node);

            if (!transformer.TryTransform(consume, out string error))
            {
                throw new Exception(error);
            }

            Visit(in consume, in script);
        }
        private SelectExpression TransformConsumeToFilter(in ConsumeStatement consume)
        {
            SelectExpression select = new()
            {
                Hints = "FOR UPDATE SKIP LOCKED"
            };

            foreach (OrderExpression order in consume.Order.Expressions)
            {
                if (order.Expression is ColumnReference column)
                {
                    if (column.Binding is MetadataProperty property)
                    {
                        select.Select.Add(new ColumnExpression()
                        {
                            Expression = new ColumnReference()
                            {
                                Binding = property,
                                Identifier = column.Identifier
                            }
                        });
                    }
                }
            }

            select.Top = consume.Top;
            select.From = consume.From;
            select.Where = consume.Where;
            select.Order = consume.Order;

            return select;
        }
        private SelectStatement TransformConsumeToSelect(in ConsumeStatement consume)
        {
            SelectExpression filter = TransformConsumeToFilter(in consume);

            CommonTableExpression cte = new() { Name = "filter", Expression = filter };

            SelectExpression select = new();

            foreach (ColumnExpression output in consume.Columns)
            {
                if (output.Expression is ColumnReference column)
                {
                    select.Select.Add(new ColumnExpression()
                    {
                        Alias = output.Alias,
                        Expression = new ColumnReference()
                        {
                            Binding = column.Binding,
                            Identifier = "source." + column.Identifier
                        }
                    });
                }
                else if (output.Expression is FunctionExpression function && function.Token == TokenType.DATALENGTH)
                {
                    if (function.Parameters.Count > 0 && function.Parameters[0] is ColumnReference parameter)
                    {
                        select.Select.Add(new ColumnExpression()
                        {
                            Alias = output.Alias,
                            Expression = new FunctionExpression()
                            {
                                Name = function.Name,
                                Token = function.Token,
                                Parameters = new List<SyntaxNode>()
                                {
                                    new ColumnReference()
                                    {
                                        Binding = parameter.Binding,
                                        Identifier = "source." + parameter.Identifier
                                    }
                                }
                            }
                        });
                    }
                }
            }

            if (consume.From.Expression is TableReference table)
            {
                TableReference source = new() { Alias = "source", Identifier = table.Identifier, Binding = table.Binding };

                select.From = new FromClause()
                {
                    Expression = new TableJoinOperator()
                    {
                        Token = TokenType.INNER,
                        Expression1 = source,
                        Expression2 = new TableReference() { Identifier = "filter", Binding = cte },
                        On = new OnClause()
                        {
                            Expression = TransformOrderToWhere(consume.Order, in filter)
                        }
                    }
                };
            }

            if (consume.Order is not null)
            {
                select.Order = new OrderClause();

                foreach (OrderExpression order in consume.Order.Expressions)
                {
                    if (order.Expression is ColumnReference column)
                    {
                        select.Order.Expressions.Add(new OrderExpression()
                        {
                            Token = order.Token,
                            Expression = new ColumnReference()
                            {
                                Binding = column.Binding,
                                Identifier = "source." + column.Identifier
                            }
                        });
                    }
                }
            }

            return new SelectStatement() { Select = select, CommonTables = cte };
        }
        private GroupOperator TransformOrderToWhere(in OrderClause order, in SelectExpression filter)
        {
            GroupOperator group = new();

            foreach (OrderExpression expression in order.Expressions)
            {
                if (expression.Expression is ColumnReference column)
                {
                    if (group.Expression == null)
                    {
                        group.Expression = CreateEqualityComparisonOperator(in column, in filter);
                    }
                    else
                    {
                        group.Expression = new BinaryOperator()
                        {
                            Token = TokenType.AND,
                            Expression1 = group.Expression,
                            Expression2 = CreateEqualityComparisonOperator(in column, in filter)
                        };
                    }
                }
            }

            return group;
        }
        private ComparisonOperator CreateEqualityComparisonOperator(in ColumnReference column, in SelectExpression filter)
        {
            if (column.Binding is not MetadataProperty property) { return null; }

            ColumnReference column1 = new()
            {
                Binding = property,
                Identifier = "source." + column.Identifier
            };

            ColumnReference column2 = new() { Identifier = "filter." + property.Name };

            BindColumn(in filter, property.Name, in column2);

            ComparisonOperator comparison = new()
            {
                Token = TokenType.Equals,
                Expression1 = column1,
                Expression2 = column2
            };

            return comparison;
        }
    }
}

// Шаблон запроса на деструктивное чтение для PostgreSQL
//WITH filter AS
//(SELECT
//  МоментВремени,
//  Идентификатор
//FROM
//  {TABLE_NAME}
//ORDER BY
//  МоментВремени ASC,
//  Идентификатор ASC
//LIMIT
//  @MessageCount
//FOR UPDATE SKIP LOCKED
//),

//queue AS(
//DELETE FROM {TABLE_NAME} t USING filter
//WHERE t.МоментВремени = filter.МоментВремени
//  AND t.Идентификатор = filter.Идентификатор
//RETURNING
//  t.МоментВремени, t.Идентификатор, t.ДатаВремя,
//  t.Отправитель, t.Получатели, t.Заголовки,
//  t.ТипОперации, t.ТипСообщения, t.ТелоСообщения
//)

//SELECT
//  queue.МоментВремени, queue.Идентификатор, queue.ДатаВремя,
//  CAST(queue.Заголовки     AS text)    AS "Заголовки",
//  CAST(queue.Отправитель   AS varchar) AS "Отправитель",
//  CAST(queue.Получатели    AS text)    AS "Получатели",
//  CAST(queue.ТипОперации   AS varchar) AS "ТипОперации",
//  CAST(queue.ТипСообщения  AS varchar) AS "ТипСообщения",
//  CAST(queue.ТелоСообщения AS text)    AS "ТелоСообщения"
//FROM
//  queue
//ORDER BY
//  queue.МоментВремени ASC,
//  queue.Идентификатор ASC
//;