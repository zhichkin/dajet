using DaJet.Data;
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
            if (select.From is null) { return false; }

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

            if (!string.IsNullOrEmpty(node.Target.Alias))
            {
                script.Append(" AS ").Append(node.Target.Alias);
            }

            if (!string.IsNullOrEmpty(node.Using))
            {
                script.Append(" USING ").Append(node.Using);
            }
            else if (node.CommonTables is not null)
            {
                //TODO: (pg DELETE) find cte names in WHERE clause
                script.Append(" USING ").Append(node.CommonTables.Name);
            }

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
            SelectExpression select = TransformConsumeToFilter(in node);

            CommonTableExpression filter = new() { Name = "filter", Expression = select };

            DeleteStatement delete = TransformConsumeToDelete(in node, in filter);

            CommonTableExpression queue = new()
            {
                Next = filter, Name = "queue", Expression = delete
            };

            SelectStatement consume = TransformConsumeToSelect(in filter, in queue);

            consume.CommonTables = queue;

            Visit(in consume, in script);
        }
        private SelectExpression TransformConsumeToFilter(in ConsumeStatement consume)
        {
            ColumnReferenceTransformer transformer = new();

            SelectExpression select = new()
            {
                Hints = "FOR UPDATE SKIP LOCKED"
            };

            foreach (OrderExpression order in consume.Order.Expressions)
            {
                if (order.Expression is not ColumnReference column) { continue; }

                ColumnExpression expression = new()
                {
                    Expression = new ColumnReference()
                    {
                        Binding = column.Binding,
                        Identifier = column.Identifier
                    }
                };

                transformer.Transform(expression);

                select.Select.Add(expression);
            }
            
            select.Top = consume.Top;
            select.From = consume.From;
            select.Where = consume.Where;
            select.Order = consume.Order;

            return select;
        }
        private DeleteStatement TransformConsumeToDelete(in ConsumeStatement consume, in CommonTableExpression filter)
        {
            DeleteStatement delete = new()
            {
                Using = filter.Name,
                Output = new OutputClause()
            };

            if (consume.From.Expression is not TableReference table) { return delete; }

            delete.Target = new TableReference()
            {
                Alias = "source",
                Binding = table.Binding,
                Identifier = table.Identifier
            };

            if (filter.Expression is SelectExpression select)
            {
                delete.Where = new WhereClause()
                {
                    Expression = TransformFilterSelectToDeleteWhere(in select)
                };
            }

            foreach (ColumnExpression output in consume.Columns)
            {
                if (output.Expression is ColumnReference column)
                {
                    ColumnExpression expression = new() { Alias = output.Alias };

                    ColumnReference reference = new()
                    {
                        Binding = column.Binding,
                        Identifier = "source." + column.Identifier
                    };

                    if (column.Mapping is not null)
                    {
                        reference.Mapping = new List<ColumnMap>();

                        foreach (ColumnMap map in column.Mapping)
                        {
                            reference.Mapping.Add(new ColumnMap()
                            {
                                Name = "source." + map.Name,
                                Type = map.Type,
                                Alias = map.Alias
                            });
                        }
                    }

                    expression.Expression = reference;

                    delete.Output.Columns.Add(expression);
                }
                else if (output.Expression is FunctionExpression function && function.Token == TokenType.DATALENGTH)
                {
                    if (function.Parameters.Count > 0 && function.Parameters[0] is ColumnReference parameter)
                    {
                        ColumnExpression expression = new() { Alias = output.Alias };

                        ColumnReference reference = new()
                        {
                            Binding = parameter.Binding,
                            Identifier = "source." + parameter.Identifier
                        };

                        if (parameter.Mapping is not null)
                        {
                            reference.Mapping = new List<ColumnMap>();

                            foreach (ColumnMap map in parameter.Mapping)
                            {
                                reference.Mapping.Add(new ColumnMap()
                                {
                                    Name = "source." + map.Name,
                                    Type = map.Type,
                                    Alias = map.Alias
                                });
                            }
                        }

                        expression.Expression = new FunctionExpression()
                        {
                            Name = function.Name,
                            Token = TokenType.DATALENGTH,
                            Parameters = new List<SyntaxNode>() { reference }
                        };

                        delete.Output.Columns.Add(expression);
                    }
                }
            }

            //TODO: add columns from ORDER clause if consume.Columns do not contain them !!!

            return delete;
        }
        private SelectStatement TransformConsumeToSelect(in CommonTableExpression filter, in CommonTableExpression queue)
        {
            SelectStatement statement = new();

            SelectExpression select = new()
            {
                From = new FromClause()
                {
                    Expression = new TableReference() { Identifier = "queue", Binding = queue }
                }
            };

            if (queue.Expression is not DeleteStatement delete) { return statement; }

            foreach (ColumnExpression output in delete.Output.Columns)
            {
                if (output.Expression is ColumnReference column)
                {
                    ColumnExpression expression = new() { Alias = output.Alias };

                    ColumnReference reference = new()
                    {
                        Binding = output,
                        Identifier = "queue." + output.Alias
                    };

                    if (column.Mapping is not null)
                    {
                        reference.Mapping = new List<ColumnMap>();

                        foreach (ColumnMap map in column.Mapping)
                        {
                            reference.Mapping.Add(new ColumnMap()
                            {
                                Type = map.Type,
                                Name = "queue." + map.Alias
                            });
                        }
                    }

                    expression.Expression = reference;

                    select.Select.Add(expression);
                }
                else if (output.Expression is FunctionExpression function && function.Token == TokenType.DATALENGTH)
                {
                    if (function.Parameters.Count > 0 && function.Parameters[0] is ColumnReference parameter)
                    {
                        ColumnExpression expression = new() { Alias = output.Alias };

                        ColumnReference reference = new()
                        {
                            Binding = parameter.Binding,
                            Identifier = "queue." + output.Alias
                        };

                        if (parameter.Mapping is not null)
                        {
                            reference.Mapping = new List<ColumnMap>();

                            foreach (ColumnMap map in parameter.Mapping)
                            {
                                reference.Mapping.Add(new ColumnMap()
                                {
                                    Type = map.Type,
                                    Name = "queue." + output.Alias
                                });
                            }
                        }

                        expression.Expression = reference;

                        select.Select.Add(expression);
                    }
                }
            }
            
            if (filter.Expression is SelectExpression fields)
            {
                select.Order = new OrderClause();

                foreach (ColumnExpression order in fields.Select)
                {
                    if (order.Expression is ColumnReference column)
                    {
                        ColumnReference reference = new()
                        {
                            Binding = order,
                            Identifier = "queue." + column.Identifier
                        };

                        if (column.Mapping is not null)
                        {
                            reference.Mapping = new List<ColumnMap>();

                            foreach (ColumnMap map in column.Mapping)
                            {
                                reference.Mapping.Add(new ColumnMap()
                                {
                                    Type = map.Type,
                                    Name = "queue." + map.Alias
                                });
                            }
                        }

                        select.Order.Expressions.Add(new OrderExpression()
                        {
                            Expression = reference, Token = TokenType.ASC
                        });
                    }
                }
            }

            statement.Select = select;

            return statement;
        }
        private GroupOperator TransformFilterSelectToDeleteWhere(in SelectExpression filter)
        {
            GroupOperator group = new();

            foreach (ColumnExpression expression in filter.Select)
            {
                if (expression.Expression is not ColumnReference column) { continue; }

                if (group.Expression == null)
                {
                    group.Expression = CreateEqualityComparisonOperator(in expression, in column);
                }
                else
                {
                    group.Expression = new BinaryOperator()
                    {
                        Token = TokenType.AND,
                        Expression1 = group.Expression,
                        Expression2 = CreateEqualityComparisonOperator(in expression, in column)
                    };
                }
            }

            return group;
        }
        private ComparisonOperator CreateEqualityComparisonOperator(in ColumnExpression property, in ColumnReference column)
        {
            ColumnReference column1 = new()
            {
                Binding = column.Binding,
                Identifier = "source." + column.Identifier
            };

            ColumnReference column2 = new()
            {
                Binding = property,
                Identifier = "filter." + property.Alias
            };

            if (column.Mapping is not null)
            {
                column1.Mapping = new List<ColumnMap>();
                column2.Mapping = new List<ColumnMap>();

                foreach (ColumnMap map in column.Mapping)
                {
                    column1.Mapping.Add(new ColumnMap()
                    {
                        Type = map.Type,
                        Name = "source." + map.Name
                    });

                    column2.Mapping.Add(new ColumnMap()
                    {
                        Type = map.Type,
                        Name = "filter." + map.Alias
                    });
                }
            }
            
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