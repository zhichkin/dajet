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
                script.AppendLine().Append(node.Hints);
            }
        }
        protected override void Visit(in TopClause node, in StringBuilder script)
        {
            script.AppendLine().Append("LIMIT ");
            Visit(node.Expression, in script);
        }
        protected override void Visit(in TableReference node, in StringBuilder script)
        {
            if (node.Binding is EntityDefinition table)
            {
                script.Append("UNNEST(").Append(table.TableName).Append(')');

                if (!string.IsNullOrEmpty(node.Alias))
                {
                    script.Append(" AS ").Append(node.Alias);
                }
            }
            else
            {
                base.Visit(in node, in script);
            }
        }
        protected override void Visit(in List<ColumnMap> mapping, in StringBuilder script)
        {
            ColumnMap column;

            for (int i = 0; i < mapping.Count; i++)
            {
                column = mapping[i];

                if (i > 0) { script.Append(", "); }

                if (column.TypeName.StartsWith("char") || column.TypeName.StartsWith("text"))
                {
                    script.Append("CAST(").Append(column.Name).Append(" AS mvarchar)"); // table variable column trick
                }
                else
                {
                    script.Append(column.Name);
                }

                if (!string.IsNullOrEmpty(column.Alias))
                {
                    script.Append(" AS ").Append(column.Alias);
                }
            }
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
            string name = node.Name.ToUpperInvariant();

            if (name == "TYPEOF" || name == "UUIDOF")
            {
                base.Visit(in node, in script); return;
            }
            else if (name == "ISNULL")
            {
                node.Name = "COALESCE";
            }
            else if (name == "DATALENGTH")
            {
                node.Name = "OCTET_LENGTH";
                script.Append(node.Name).Append('(');
                script.Append("CAST(");
                Visit(node.Parameters[0], in script);
                script.Append(" AS text)");
                script.Append(')');
                return; //TODO: OCTET_LENGTH - what if data type of column is bytea ?
            }
            else if (name == "NOW")
            {
                script.Append("NOW()::timestamp"); return;
            }

            script.Append(node.Name).Append("(");

            SyntaxNode expression;

            for (int i = 0; i < node.Parameters.Count; i++)
            {
                expression = node.Parameters[i];
                if (i > 0) { script.Append(", "); }

                if (name == "SUBSTRING" && i == 0)
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

        #region "DELETE STATEMENT"
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

            if (node.Where is not null)
            {
                string source = string.IsNullOrEmpty(node.Target.Alias) ? node.Target.Identifier : node.Target.Alias;

                if (GetCommonTableReferences(node.Where, in source, out List<string> tables))
                {
                    script.Append(" USING");

                    for (int i = 0; i < tables.Count; i++)
                    {
                        if (i > 0) { script.Append(','); }

                        script.Append(' ').Append(tables[i]);
                    }
                }
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
        private bool GetCommonTableReferences(in WhereClause where, in string source, out List<string> identifiers)
        {
            identifiers = new List<string>();

            Visit(where.Expression, in source, in identifiers);

            return (identifiers.Count > 0);
        }
        private void Visit(in SyntaxNode node, in string source, in List<string> identifiers)
        {
            if (node is ComparisonOperator comparison)
            {
                Visit(in comparison, in source, in identifiers);
            }
            else if (node is ColumnReference column)
            {
                Visit(in column, in source, in identifiers);
            }
            else if (node is GroupOperator group)
            {
                Visit(in group, in source, in identifiers);
            }
            else if (node is UnaryOperator unary)
            {
                Visit(in unary, in source, in identifiers);
            }
            else if (node is BinaryOperator binary)
            {
                Visit(in binary, in source, in identifiers);
            }
        }
        private void Visit(in ColumnReference column, in string source, in List<string> identifiers)
        {
            ScriptHelper.GetColumnIdentifiers(column.Identifier, out string table, out _);

            if (string.IsNullOrWhiteSpace(table) || table == source) { return; }

            if (!identifiers.Contains(table))
            {
                identifiers.Add(table);
            }
        }
        private void Visit(in GroupOperator _operator, in string source, in List<string> identifiers)
        {
            Visit(_operator.Expression, in source, in identifiers);
        }
        private void Visit(in UnaryOperator _operator, in string source, in List<string> identifiers)
        {
            Visit(_operator.Expression, in source, in identifiers);
        }
        private void Visit(in BinaryOperator _operator, in string source, in List<string> identifiers)
        {
            Visit(_operator.Expression1, in source, in identifiers);
            Visit(_operator.Expression2, in source, in identifiers);
        }
        private void Visit(in ComparisonOperator _operator, in string source, in List<string> identifiers)
        {
            Visit(_operator.Expression1, in source, in identifiers);
            Visit(_operator.Expression2, in source, in identifiers);
        }
        #endregion

        #region "CONSUME STATEMENT"
        protected override void Visit(in ConsumeStatement node, in StringBuilder script)
        {
            if (!TryGetConsumeTargetTable(node.From, out TableReference table))
            {
                throw new InvalidOperationException("CONSUME: target table is not found.");
            }

            IndexInfo index = GetPrimaryOrUniqueIndex(in table) ?? throw new InvalidOperationException("CONSUME: target table has no valid index.");

            SelectStatement select;

            if (node.From.Expression is TableReference)
            {
                select = TransformSimpleConsume(in node, in table, in index); // single target table
            }
            else
            {
                select = TransformComplexConsume(in node, in table, in index); // target table with JOIN(s)
            }
            
            Visit(in select, in script);
        }
        private IndexInfo GetPrimaryOrUniqueIndex(in TableReference table)
        {
            if (table.Binding is not ApplicationObject entity)
            {
                throw new InvalidOperationException("CONSUME: target table has no entity binding.");
            }

            string target = entity.TableName.ToLowerInvariant();

            List<IndexInfo> indexes = new PgSqlHelper().GetIndexes(Metadata.ConnectionString, target);

            foreach (IndexInfo index in indexes)
            {
                if (index.IsPrimary) { return index; }
            }

            foreach (IndexInfo index in indexes)
            {
                if (index.IsUnique && index.IsClustered) { return index; }
            }

            foreach (IndexInfo index in indexes)
            {
                if (index.IsUnique) { return index; }
            }

            return null;
        }

        #region "CONSUME FROM ONE TARGET TABLE"
        private SelectStatement TransformSimpleConsume(in ConsumeStatement node, in TableReference table, in IndexInfo index)
        {
            SelectExpression select = TransformConsumeToFilter(in node, in table, in index);

            CommonTableExpression filter = new() { Name = "filter", Expression = select };

            DeleteStatement delete = TransformConsumeToDelete(in node, in table, in filter, out List<OrderExpression> order);

            CommonTableExpression queue = new()
            {
                Next = filter,
                Name = "queue",
                Expression = delete
            };

            SelectStatement consume = TransformConsumeToSelect(in node, in queue, in order);

            consume.CommonTables = queue;

            return consume;
        }
        private SelectExpression TransformConsumeToFilter(in ConsumeStatement consume, in TableReference table, in IndexInfo index)
        {
            SelectExpression select = new()
            {
                Hints = "FOR UPDATE" + (consume.StrictOrderRequired ? string.Empty: " SKIP LOCKED")
            };

            foreach (IndexColumnInfo column in index.Columns)
            {
                select.Select.Add(new ColumnExpression()
                {
                    Expression = new ColumnReference()
                    {
                        Binding = column,
                        Identifier = column.Name,
                        Mapping = new List<ColumnMap>()
                        {
                            new ColumnMap()
                            {
                                Name = column.Name
                            }
                        }
                    }
                });
            }
            
            select.Top = consume.Top;
            select.From = new FromClause() { Expression = table };
            select.Where = consume.Where;
            select.Order = consume.Order;

            return select;
        }
        private DeleteStatement TransformConsumeToDelete(in ConsumeStatement consume, in TableReference table, in CommonTableExpression filter, out List<OrderExpression> order)
        {
            DeleteStatement delete = new()
            {
                Output = new OutputClause()
            };

            order = new List<OrderExpression>();

            delete.Target = new TableReference()
            {
                Alias = "source",
                Binding = table.Binding,
                Identifier = table.Identifier
            };

            // WHERE clause - target table columns only !!!

            if (filter.Expression is SelectExpression select)
            {
                delete.Where = new WhereClause()
                {
                    Expression = TransformFilterSelectToDeleteWhere(in select)
                };
            }

            // RETURNING clause - target table columns only !!!

            foreach (ColumnExpression output in consume.Columns)
            {
                if (output.Expression is ColumnReference column)
                {
                    ColumnExpression expression = new() { Alias = output.Alias };

                    // 1. Ссылка => source._idrref
                    // 2. Изменения.Ссылка => source._idrref
                    // 3. Изменения.Ссылка AS Ссылка => source.Ссылка

                    ScriptHelper.GetColumnIdentifiers(column.Identifier, out _, out string columnName);

                    if (!string.IsNullOrEmpty(output.Alias))
                    {
                        columnName = output.Alias;
                    }

                    ColumnReference reference = new()
                    {
                        Binding = column.Binding,
                        Identifier = "source." + columnName
                    };

                    if (column.Mapping is not null)
                    {
                        reference.Mapping = new List<ColumnMap>();

                        foreach (ColumnMap map in column.Mapping)
                        {
                            ScriptHelper.GetColumnIdentifiers(map.Name, out _, out columnName);

                            reference.Mapping.Add(new ColumnMap()
                            {
                                Name = "source." + columnName,
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

            // ORDER BY clause for final SELECT statement

            if (consume.Order is not null)
            {
                foreach (OrderExpression consumeOrder in consume.Order.Expressions)
                {
                    if (consumeOrder.Expression is ColumnReference orderColumn)
                    {
                        bool found = false;

                        foreach (ColumnExpression expression in consume.Columns)
                        {
                            if (expression.Expression is ColumnReference selectColumn)
                            {
                                if (orderColumn.Identifier == selectColumn.Identifier)
                                {
                                    found = true;

                                    ColumnReference queueOrder = new()
                                    {
                                        Binding = selectColumn.Binding,
                                        Identifier = selectColumn.Identifier
                                    };

                                    if (selectColumn.Mapping is not null)
                                    {
                                        queueOrder.Mapping = new List<ColumnMap>();

                                        foreach (ColumnMap map in selectColumn.Mapping)
                                        {
                                            queueOrder.Mapping.Add(new ColumnMap()
                                            {
                                                Type = map.Type,
                                                Name = string.IsNullOrEmpty(map.Alias) ? map.Name : map.Alias
                                            });
                                        }
                                    }

                                    order.Add(new OrderExpression()
                                    {
                                        Token = consumeOrder.Token,
                                        Expression = queueOrder
                                    });

                                    break;
                                }
                            }
                        }

                        if (!found) // add order columns to OUTPUT clause to be used by final SELECT statement in ORDER clause
                        {
                            ColumnExpression expression = new();

                            ColumnReference reference = new()
                            {
                                Binding = orderColumn.Binding,
                                Identifier = orderColumn.Identifier
                            };

                            if (orderColumn.Mapping is not null)
                            {
                                reference.Mapping = new List<ColumnMap>();

                                foreach (ColumnMap map in orderColumn.Mapping)
                                {
                                    reference.Mapping.Add(new ColumnMap()
                                    {
                                        Type = map.Type,
                                        Name = "source." + map.Name
                                    });
                                }
                            }

                            expression.Expression = reference;

                            delete.Output.Columns.Add(expression);

                            // order column

                            ColumnReference queueOrder = new()
                            {
                                Binding = orderColumn.Binding,
                                Identifier = orderColumn.Identifier
                            };

                            if (orderColumn.Mapping is not null)
                            {
                                queueOrder.Mapping = new List<ColumnMap>();

                                foreach (ColumnMap map in orderColumn.Mapping)
                                {
                                    queueOrder.Mapping.Add(new ColumnMap()
                                    {
                                        Type = map.Type,
                                        Name = map.Name
                                    });
                                }
                            }

                            order.Add(new OrderExpression()
                            {
                                Token = consumeOrder.Token,
                                Expression = queueOrder
                            });
                        }
                    }
                }
            }

            return delete;
        }
        private SelectStatement TransformConsumeToSelect(in ConsumeStatement consume, in CommonTableExpression queue, in List<OrderExpression> order)
        {
            SelectStatement statement = new();

            SelectExpression select = new()
            {
                From = new FromClause()
                {
                    Expression = new TableReference() { Identifier = "queue", Binding = queue }
                }
            };

            statement.Select = select;

            foreach (ColumnExpression property in consume.Columns)
            {
                if (property.Expression is ColumnReference column)
                {
                    ColumnExpression expression = new() { Alias = property.Alias };

                    ColumnReference reference = new()
                    {
                        Binding = property,
                        Identifier = "queue." + property.Alias
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
                else if (property.Expression is FunctionExpression function && function.Token == TokenType.DATALENGTH)
                {
                    if (function.Parameters.Count > 0 && function.Parameters[0] is ColumnReference parameter)
                    {
                        ColumnExpression expression = new() { Alias = property.Alias };

                        ColumnReference reference = new()
                        {
                            Binding = parameter.Binding,
                            Identifier = "queue." + property.Alias
                        };

                        if (parameter.Mapping is not null)
                        {
                            reference.Mapping = new List<ColumnMap>();

                            foreach (ColumnMap map in parameter.Mapping)
                            {
                                reference.Mapping.Add(new ColumnMap()
                                {
                                    Type = map.Type,
                                    Name = "queue." + property.Alias
                                });
                            }
                        }

                        expression.Expression = reference;

                        select.Select.Add(expression);
                    }
                }
            }

            if (order is not null)
            {
                select.Order = new OrderClause();

                foreach (OrderExpression expression in order)
                {
                    if (expression.Expression is ColumnReference column)
                    {
                        ColumnReference reference = new()
                        {
                            Binding = column.Binding,
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
                                    Name = "queue." + map.Name
                                });
                            }
                        }

                        select.Order.Expressions.Add(new OrderExpression()
                        {
                            Token = expression.Token,
                            Expression = reference
                        });
                    }
                }
            }

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
                Identifier = "filter." + column.Identifier
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
                        Name = "filter." + map.Name
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
        #endregion

        #region "CONSUME FROM TARGET TABLE WITH JOIN(S)"
        private SelectStatement TransformComplexConsume(in ConsumeStatement node, in TableReference table, in IndexInfo index)
        {
            SelectExpression select = TransformConsumeToSelect(in node, in table, in index, out List<ColumnExpression> filter, out List<ColumnExpression> output);

            CommonTableExpression changes = new() { Name = "changes", Expression = select };

            DeleteStatement delete = TransformConsumeToDelete(in table, in filter, in output);

            CommonTableExpression source = new()
            {
                Next = changes,
                Name = "source",
                Expression = delete
            };

            SelectStatement consume = TransformConsumeToSelect(in node, in source);

            consume.CommonTables = source;

            return consume;
        }
        private SelectExpression TransformConsumeToSelect(in ConsumeStatement consume, in TableReference table, in IndexInfo index, out List<ColumnExpression> filter, out List<ColumnExpression> output)
        {
            string rowLocking = (consume.StrictOrderRequired ? string.Empty : " SKIP LOCKED");
            string targetName = (string.IsNullOrEmpty(table.Alias) ? string.Empty : table.Alias);
            string targetTable = (string.IsNullOrEmpty(table.Alias) ? table.Identifier : table.Alias);
            
            SelectExpression select = new() { Hints = "FOR UPDATE OF " + targetTable + rowLocking };

            filter = new List<ColumnExpression>();
            output = new List<ColumnExpression>();

            foreach (IndexColumnInfo column in index.Columns)
            {
                ColumnExpression filterColumn = new()
                {
                    Alias = column.Name,
                    Expression = new ColumnReference()
                    {
                        Binding = column,
                        Identifier = targetName + "." + column.Name,
                        Mapping = new List<ColumnMap>()
                        {
                            new ColumnMap()
                            {
                                Alias = column.Name,
                                Name = targetName + "." + column.Name,
                            }
                        }
                    }
                };

                filter.Add(filterColumn);
                select.Select.Add(filterColumn);
            }

            CreateConsumeOrder(in consume, in select, in output);

            foreach (ColumnExpression outputColumn in consume.Columns)
            {
                output.Add(outputColumn);
                select.Select.Add(outputColumn);
            }

            select.Top = consume.Top;
            select.From = consume.From;
            select.Where = consume.Where;
            select.Order = consume.Order;

            return select;
        }
        private void CreateConsumeOrder(in ConsumeStatement consume, in SelectExpression select, in List<ColumnExpression> output)
        {
            if (consume.Order is null) { return; }

            foreach (OrderExpression consumeOrder in consume.Order.Expressions)
            {
                if (consumeOrder.Expression is ColumnReference orderColumn)
                {
                    bool found = false;

                    foreach (ColumnExpression expression in consume.Columns)
                    {
                        if (expression.Expression is ColumnReference selectColumn)
                        {
                            if (orderColumn.Identifier == selectColumn.Identifier)
                            {
                                found = true; break;
                            }
                        }
                    }

                    if (found) { continue; }

                    ScriptHelper.GetColumnIdentifiers(orderColumn.Identifier, out _, out string columnName);

                    ColumnReference reference = new()
                    {
                        Binding = orderColumn.Binding,
                        Identifier = orderColumn.Identifier
                    };

                    ColumnExpression outputColumn = new()
                    {
                        Alias = columnName,
                        Expression = reference
                    };

                    if (orderColumn.Mapping is not null)
                    {
                        reference.Mapping = new List<ColumnMap>();

                        foreach (ColumnMap map in orderColumn.Mapping)
                        {
                            reference.Mapping.Add(new ColumnMap()
                            {
                                Alias = columnName,
                                Name = map.Name,
                                Type = map.Type,
                                TypeName = map.TypeName
                            });
                        }
                    }

                    output.Add(outputColumn);
                    select.Select.Add(outputColumn);
                }
            }
        }
        private DeleteStatement TransformConsumeToDelete(in TableReference table, in List<ColumnExpression> index, in List<ColumnExpression> output)
        {
            DeleteStatement delete = new()
            {
                Output = new OutputClause(),
                Target = new TableReference()
                {
                    Alias = "target",
                    Binding = table.Binding,
                    Identifier = table.Identifier
                },
                Where = new WhereClause()
                {
                    Expression = CreateDeletionFilter(in index)
                }
            };

            // RETURNING clause - CONSUME output columns

            foreach (ColumnExpression outputColumn in output)
            {
                if (outputColumn.Expression is ColumnReference column)
                {
                    ColumnExpression expression = new() { Alias = outputColumn.Alias };

                    // 1. Ссылка                     => changes.Ссылка
                    // 2. Изменения.Ссылка           => changes.Ссылка
                    // 3. Изменения.Ссылка AS Ссылка => changes.Ссылка

                    ScriptHelper.GetColumnIdentifiers(column.Identifier, out _, out string columnName);
                    if (!string.IsNullOrEmpty(outputColumn.Alias)) { columnName = outputColumn.Alias; }

                    ColumnReference reference = new()
                    {
                        Binding = column.Binding,
                        Identifier = "changes." + columnName
                    };

                    if (column.Mapping is not null)
                    {
                        reference.Mapping = new List<ColumnMap>();

                        foreach (ColumnMap map in column.Mapping)
                        {
                            ScriptHelper.GetColumnIdentifiers(map.Name, out _, out columnName);
                            if (!string.IsNullOrEmpty(map.Alias)) { columnName = map.Alias; }

                            reference.Mapping.Add(new ColumnMap()
                            {
                                Type = map.Type,
                                Name = "changes." + columnName
                            });
                        }
                    }

                    expression.Expression = reference;

                    delete.Output.Columns.Add(expression);
                }
                else if (outputColumn.Expression is FunctionExpression function && function.Token == TokenType.DATALENGTH)
                {
                    if (function.Parameters.Count > 0 && function.Parameters[0] is ColumnReference parameter)
                    {
                        ColumnExpression expression = new() { Alias = outputColumn.Alias };

                        ColumnReference reference = new()
                        {
                            Binding = parameter.Binding,
                            Identifier = "changes." + parameter.Identifier
                        };

                        if (parameter.Mapping is not null)
                        {
                            reference.Mapping = new List<ColumnMap>();

                            foreach (ColumnMap map in parameter.Mapping)
                            {
                                reference.Mapping.Add(new ColumnMap()
                                {
                                    Name = "changes." + map.Name,
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

            return delete;
        }
        private GroupOperator CreateDeletionFilter(in List<ColumnExpression> filter)
        {
            GroupOperator group = new();

            foreach (ColumnExpression expression in filter)
            {
                if (expression.Expression is not ColumnReference column) { continue; }

                if (group.Expression == null)
                {
                    group.Expression = CreateDeletionFilterOperator(in expression, in column);
                }
                else
                {
                    group.Expression = new BinaryOperator()
                    {
                        Token = TokenType.AND,
                        Expression1 = group.Expression,
                        Expression2 = CreateDeletionFilterOperator(in expression, in column)
                    };
                }
            }

            return group;
        }
        private ComparisonOperator CreateDeletionFilterOperator(in ColumnExpression property, in ColumnReference column)
        {
            ScriptHelper.GetColumnIdentifiers(column.Identifier, out _, out string columnName);

            ColumnReference column1 = new()
            {
                Binding = column.Binding,
                Identifier = "target." + columnName
            };

            ColumnReference column2 = new()
            {
                Binding = property,
                Identifier = "changes." + columnName
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
                        Name = "target." + columnName
                    });

                    column2.Mapping.Add(new ColumnMap()
                    {
                        Type = map.Type,
                        Name = "changes." + columnName
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
        private SelectStatement TransformConsumeToSelect(in ConsumeStatement consume, in CommonTableExpression output)
        {
            SelectStatement statement = new();

            SelectExpression select = new()
            {
                From = new FromClause()
                {
                    Expression = new TableReference()
                    {
                        Binding = output,
                        Identifier = "source"
                    }
                }
            };

            statement.Select = select;

            foreach (ColumnExpression property in consume.Columns)
            {
                if (property.Expression is ColumnReference column)
                {
                    ColumnExpression expression = new() { Alias = property.Alias };

                    ColumnReference reference = new()
                    {
                        Binding = property,
                        Identifier = "source." + property.Alias
                    };

                    if (column.Mapping is not null)
                    {
                        reference.Mapping = new List<ColumnMap>();

                        foreach (ColumnMap map in column.Mapping)
                        {
                            reference.Mapping.Add(new ColumnMap()
                            {
                                Type = map.Type,
                                Name = "source." + map.Alias
                            });
                        }
                    }

                    expression.Expression = reference;

                    select.Select.Add(expression);
                }
                else if (property.Expression is FunctionExpression function && function.Token == TokenType.DATALENGTH)
                {
                    if (function.Parameters.Count > 0 && function.Parameters[0] is ColumnReference parameter)
                    {
                        ColumnExpression expression = new() { Alias = property.Alias };

                        ColumnReference reference = new()
                        {
                            Binding = parameter.Binding,
                            Identifier = "source." + property.Alias
                        };

                        if (parameter.Mapping is not null)
                        {
                            reference.Mapping = new List<ColumnMap>();

                            foreach (ColumnMap map in parameter.Mapping)
                            {
                                reference.Mapping.Add(new ColumnMap()
                                {
                                    Type = map.Type,
                                    Name = "source." + property.Alias
                                });
                            }
                        }

                        expression.Expression = reference;

                        select.Select.Add(expression);
                    }
                }
            }

            if (consume.Order is not null)
            {
                select.Order = new OrderClause();

                foreach (OrderExpression expression in consume.Order.Expressions)
                {
                    if (expression.Expression is ColumnReference column)
                    {
                        ScriptHelper.GetColumnIdentifiers(column.Identifier, out _, out string columnName);

                        ColumnReference reference = new()
                        {
                            Binding = column.Binding,
                            Identifier = "source." + columnName
                        };

                        if (column.Mapping is not null)
                        {
                            reference.Mapping = new List<ColumnMap>();

                            foreach (ColumnMap map in column.Mapping)
                            {
                                reference.Mapping.Add(new ColumnMap()
                                {
                                    Type = map.Type,
                                    Name = "source." + columnName
                                });
                            }
                        }

                        select.Order.Expressions.Add(new OrderExpression()
                        {
                            Token = expression.Token,
                            Expression = reference
                        });
                    }
                }
            }

            return statement;
        }
        #endregion

        #endregion // CONSUME STATEMENT

        // User-defined types and table-valued parameters
        public override void Visit(in CreateTypeStatement node, in StringBuilder script)
        {
            script.Append("CREATE TYPE \"").Append(node.Identifier).AppendLine("\" AS");
            script.AppendLine("(");

            for (int i = 0; i < node.Columns.Count; i++)
            {
                ColumnDefinition column = node.Columns[i];

                if (column.Type is not TypeIdentifier info)
                {
                    continue;
                }

                if (i > 0) { script.AppendLine(","); }

                if (info.Binding is Type type)
                {
                    if (type == typeof(bool)) // boolean
                    {
                        script.Append('"').Append('_').Append(column.Name).Append("_l").Append('"').Append(' ').Append("boolean");
                    }
                    else if (type == typeof(decimal)) // number(p,s)
                    {
                        script.Append('"').Append('_').Append(column.Name).Append("_n").Append('"')
                            .Append(' ').Append("numeric(").Append(info.Qualifier1).Append(',').Append(info.Qualifier2).Append(')');
                    }
                    else if (type == typeof(DateTime)) // datetime
                    {
                        script.Append('"').Append('_').Append(column.Name).Append("_t").Append('"').Append(' ').Append("timestamp without time zone");
                    }
                    else if (type == typeof(string)) // string(n)
                    {
                        script.Append('"').Append('_').Append(column.Name).Append("_s").Append('"').Append(' ')
                            .Append((info.Qualifier1 > 0) ? "varchar(" + info.Qualifier1.ToString() + ")" : "text");
                    }
                    else if (type == typeof(byte[])) // binary
                    {
                        script.Append('"').Append('_').Append(column.Name).Append("_b").Append('"').Append(' ').Append("bytea");
                    }
                    else if (type == typeof(Guid)) // uuid
                    {
                        script.Append('"').Append('_').Append(column.Name).Append("_u").Append('"').Append(' ').Append("bytea");
                    }
                    else if (type == typeof(Entity)) // entity - multiple reference type
                    {
                        script.Append('"').Append('_').Append(column.Name).Append("_c").Append('"').Append(' ').Append("bytea").AppendLine(",");
                        script.Append('"').Append('_').Append(column.Name).Append("_r").Append('"').Append(' ').Append("bytea");
                    }
                    else
                    {
                        throw new InvalidOperationException("Unsupported column data type");
                    }
                }
                else if (info.Binding is Entity entity) // single reference type, example: Справочник.Номенклатура
                {
                    script.Append('"').Append('_').Append(column.Name).Append("_r_").Append(entity.TypeCode).Append('"').Append(' ').Append("bytea");
                }
                else //TODO: union type
                {
                    throw new InvalidOperationException("Unknown column data type");
                    //script.Append('_').Append(column.Name).Append("_d").Append(' ').Append("bytea");
                }
            }

            script.AppendLine().AppendLine(")");
        }
    }
}

//**********************************************************************
//* Шаблон запроса на деструктивное чтение с обогащением данных (JOIN) *
//**********************************************************************
//
//WITH changes AS
//(
//  SELECT
//    Изменения._nodetref AS _nodetref,
//    Изменения._noderref AS _noderref,
//    Изменения._idrref AS _idrref,
//    ПланОбмена._Code AS Получатель,
//    Данные._IDRRef AS Ссылка,
//    Данные._Code AS Код,
//    Данные._Description AS Наименование
//        FROM _ReferenceChngR363 AS Изменения
//  INNER JOIN _Node362 AS ПланОбмена  ON (Изменения._NodeTRef = CAST(E'\\x0000016A' AS bytea) AND Изменения._NodeRRef = ПланОбмена._IDRRef)
//  LEFT  JOIN _Reference287 AS Данные ON Изменения._IDRRef = Данные._IDRRef
//  WHERE ПланОбмена._Code = CAST('DaJet' AS mvarchar)
//  ORDER BY Данные._Code ASC
//  LIMIT 10
//  FOR UPDATE OF Изменения SKIP LOCKED),
//source AS
//(
//    DELETE FROM _ReferenceChngR363 AS target USING changes
//    WHERE target._nodetref = changes._nodetref
//      AND target._noderref = changes._noderref
//      AND target._idrref   = changes._idrref
//    RETURNING changes.Получатель, changes.Ссылка, changes.Код, changes.Наименование
//)
//SELECT * FROM source ORDER BY source.Код ASC;
//
//**********************************************************************

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