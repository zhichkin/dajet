using DaJet.Data;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class MsSqlGenerator : SqlGenerator
    {
        private string GetCreateTableColumnList(in SelectExpression select)
        {
            StringBuilder columns = new();

            ColumnMap column;
            PropertyMap property;
            EntityMap map = DataMapper.CreateEntityMap(in select);

            for (int i = 0; i < map.Properties.Count; i++)
            {
                property = map.Properties[i];

                for (int ii = 0; ii < property.ColumnSequence.Count; ii++)
                {
                    column = property.ColumnSequence[ii];

                    if (column.Ordinal > 0) { columns.Append(", "); }

                    columns.Append(column.Alias).Append(' ').Append(column.TypeName);
                }
            }

            return columns.ToString();
        }
        protected override void Visit(in TableReference node, in StringBuilder script)
        {
            if (node.Binding is ApplicationObject entity)
            {
                script.Append(entity.TableName);
            }
            else if (node.Binding is TableExpression || node.Binding is CommonTableExpression)
            {
                script.Append(node.Identifier);
            }
            else if (node.Binding is TableVariableExpression)
            {
                script.Append($"@{node.Identifier}");
            }
            else if (node.Binding is TemporaryTableExpression)
            {
                script.Append($"#{node.Identifier}");
            }

            if (!string.IsNullOrEmpty(node.Alias))
            {
                script.Append(" AS ").Append(node.Alias);
            }

            if (!string.IsNullOrEmpty(node.Hints))
            {
                script.Append(' ').Append(node.Hints); // CONSUME statement support only
            }
        }
        protected override void Visit(in TableVariableExpression node, in StringBuilder script)
        {
            SelectExpression source = DataMapper.GetColumnSource(node.Expression) as SelectExpression;

            if (source is null) { return; }

            script.Append($"DECLARE @{node.Name} TABLE (");
            script.Append(GetCreateTableColumnList(in source));
            script.Append(");").AppendLine();
            script.Append($"INSERT @{node.Name}").AppendLine();

            base.Visit(in node, in script);
        }
        protected override void Visit(in TemporaryTableExpression node, in StringBuilder script)
        {
            SelectExpression source = DataMapper.GetColumnSource(node.Expression) as SelectExpression;

            if (source is null) { return; }

            script.Append($"CREATE TABLE #{node.Name} (");
            script.Append(GetCreateTableColumnList(in source));
            script.Append(");").AppendLine();
            script.Append($"INSERT #{node.Name}").AppendLine();

            base.Visit(in node, in script);
        }
        protected override void VisitTargetTable(in TableReference node, in StringBuilder script)
        {
            if (node.Binding is ApplicationObject entity)
            {
                script.Append(entity.TableName);
            }
            else if (node.Binding is TableExpression || node.Binding is CommonTableExpression)
            {
                script.Append(node.Identifier);
            }
            else if (node.Binding is TableVariableExpression)
            {
                script.Append($"@{node.Identifier}");
            }
            else if (node.Binding is TemporaryTableExpression)
            {
                script.Append($"#{node.Identifier}");
            }
            else
            {
                throw new InvalidOperationException("MS-DML: Target table identifier is missing.");
            }
        }

        protected override void Visit(in FunctionExpression node, in StringBuilder script)
        {
            string name = node.Name.ToUpperInvariant();

            if (name == "NOW") // GETUTCDATE()
            {
                if (YearOffset == 0)
                {
                    script.Append("GETDATE()");
                }
                else
                {
                    script.Append("DATEADD(year, " + YearOffset.ToString() + ", GETDATE())");
                }
            }
            else
            {
                base.Visit(in node, in script);
            }
        }

        protected override void Visit(in UpsertStatement node, in StringBuilder script)
        {
            if (node.Target.Binding is MetadataObject || node.Target.Binding is TemporaryTableExpression)
            {
                node.Hints = new() { "UPDLOCK", "SERIALIZABLE" };
            }

            base.Visit(in node, in script);
        }

        #region "DELETE STATEMENT"
        protected override void Visit(in DeleteStatement node, in StringBuilder script)
        {
            if (node.CommonTables is not null)
            {
                script.Append("WITH ");

                Visit(node.CommonTables, in script);
            }

            script.Append("DELETE ");

            VisitTargetTable(node.Target, in script);

            if (node.Output is not null)
            {
                foreach (ColumnExpression column in node.Output.Columns)
                {
                    if (column.Expression is ColumnReference reference)
                    {
                        ScriptHelper.GetColumnIdentifiers(reference.Identifier, out string tableAlias, out _);

                        if (string.IsNullOrEmpty(tableAlias))
                        {
                            reference.Identifier = "deleted." + reference.Identifier;

                            if (reference.Mapping is not null)
                            {
                                foreach (ColumnMap map in reference.Mapping)
                                {
                                    map.Name = "deleted." + map.Name;
                                }
                            }
                        }
                    }
                }

                Visit(node.Output, script);
            }

            if (node.From is not null)
            {
                Visit(node.From, script);
            }

            if (node.Where is not null)
            {
                Visit(node.Where, script);
            }
        }
        protected override void Visit(in OutputClause node, in StringBuilder script)
        {
            script.AppendLine().AppendLine("OUTPUT");

            for (int i = 0; i < node.Columns.Count; i++)
            {
                if (i > 0) { script.AppendLine(","); }

                Visit(node.Columns[i], in script);
            }

            if (node.Into is not null) { Visit(node.Into, in script); }
        }
        #endregion

        #region "CONSUME STATEMENT"
        protected override void Visit(in ConsumeStatement node, in StringBuilder script)
        {
            if (!TryGetConsumeTargetTable(node.From, out TableReference table))
            {
                throw new InvalidOperationException("CONSUME: target table is not found.");
            }

            if (node.Into is not null)
            {
                CreateTypeStatement statement = CreateTypeDefinition(node.Into.Table.Identifier, node.Columns);

                script.Insert(0, ScriptDeclareTableVariableStatement(in statement));
            }

            DeleteStatement output;

            if (node.From.Expression is TableReference)
            {
                output = TransformSimpleConsume(in node, in table);
            }
            else
            {
                output = TransformComplexConsume(in node, in table);
            }
            
            Visit(in output, in script);
        }
        private IndexInfo GetPrimaryOrUniqueIndex(in TableReference table)
        {
            if (table.Binding is not ApplicationObject entity)
            {
                throw new InvalidOperationException("CONSUME: target table has no entity binding.");
            }

            string target = entity.TableName.ToLowerInvariant();

            List<IndexInfo> indexes = new MsSqlHelper().GetIndexes(Metadata.ConnectionString, target);

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
        private string ScriptDeclareTableVariableStatement(in CreateTypeStatement statement)
        {
            StringBuilder script = new();
            
            script.Append("DECLARE @").Append(statement.Identifier).AppendLine(" AS TABLE (");

            for (int i = 0; i < statement.Columns.Count; i++)
            {
                ColumnDefinition column = statement.Columns[i];

                if (i > 0) { script.AppendLine(","); }

                script.Append(column.Name).Append(' ').Append(column.Type.Identifier);
            }
            
            script.AppendLine(");");

            return script.ToString();
        }

        #region "CONSUME FROM ONE TARGET TABLE"
        private DeleteStatement TransformSimpleConsume(in ConsumeStatement node, in TableReference table)
        {
            SelectExpression source = TransformConsumeToSelect(in node, in table);

            CommonTableExpression queue = new() { Name = "queue", Expression = source };

            return TransformConsumeToDelete(in node, in queue);
        }
        private SelectExpression TransformConsumeToSelect(in ConsumeStatement consume, in TableReference target)
        {
            target.Hints = "WITH (ROWLOCK" + (consume.StrictOrderRequired ? ")" : ", READPAST)");

            SelectExpression select = new()
            {
                Select = consume.Columns,
                Top = consume.Top,
                From = consume.From,
                Where = consume.Where,
                Order = consume.Order
            };

            return select;
        }
        private DeleteStatement TransformConsumeToDelete(in ConsumeStatement consume, in CommonTableExpression queue)
        {
            DeleteStatement delete = new()
            {
                Output = new OutputClause()
                {
                    Into = consume.Into
                },
                Target = new TableReference()
                {
                    Binding = queue,
                    Identifier = queue.Name
                },
                CommonTables = queue
            };

            foreach (ColumnExpression output in consume.Columns)
            {
                if (output.Expression is ColumnReference column)
                {
                    ColumnExpression expression = new() { Alias = output.Alias };

                    ScriptHelper.GetColumnIdentifiers(column.Identifier, out _, out string columnName);

                    ColumnReference reference = new()
                    {
                        Binding = output,
                        Identifier = "deleted." + (string.IsNullOrEmpty(output.Alias) ? columnName : output.Alias)
                    };

                    if (column.Mapping is not null)
                    {
                        reference.Mapping = new List<ColumnMap>();

                        foreach (ColumnMap map in column.Mapping)
                        {
                            reference.Mapping.Add(new ColumnMap()
                            {
                                Type = map.Type,
                                Name = "deleted." + map.Alias
                            });
                        }
                    }

                    expression.Expression = reference;

                    delete.Output.Columns.Add(expression);
                }
                else if (output.Expression is FunctionExpression function && function.Token == TokenType.DATALENGTH)
                {
                    delete.Output.Columns.Add(new ColumnExpression()
                    {
                        Expression = new ColumnReference()
                        {
                            Binding = output,
                            Identifier = "deleted." + output.Alias,
                            Mapping = new List<ColumnMap>()
                            {
                                new ColumnMap()
                                {
                                    Type = UnionTag.Integer,
                                    Name ="deleted." + output.Alias
                                }
                            }
                        }
                    });
                }
            }

            return delete;
        }
        #endregion

        #region "CONSUME FROM TARGET TABLE WITH JOIN(S)"
        private DeleteStatement TransformComplexConsume(in ConsumeStatement node, in TableReference table)
        {
            IndexInfo index = GetPrimaryOrUniqueIndex(in table) ?? throw new InvalidOperationException("CONSUME: target table has no valid index.");

            SelectExpression select = TransformConsumeToSelect(in node, in table, in index, out List<ColumnExpression> filter, out List<ColumnExpression> output);

            CommonTableExpression changes = new() { Name = "changes", Expression = select };

            DeleteStatement delete = TransformConsumeToDelete(in changes, in table, in filter, in output);

            delete.Output.Into = node.Into;

            delete.CommonTables = changes;

            //TODO: CONSUME ordering for MS SQL Server
            //TODO: 0. DECLARE @table_variable TABLE (...)
            //TODO: 1. DELETE ... OUTPUT ... INTO @table_variable
            //TODO: 2. SELECT * FROM @table_variable ORDER BY ...

            //CommonTableExpression source = new()
            //{
            //    Next = changes,
            //    Name = "source",
            //    Expression = delete
            //};

            //SelectStatement consume = TransformConsumeToSelect(in node, in source);

            //consume.CommonTables = source;

            return delete; //consume;
        }
        private SelectExpression TransformConsumeToSelect(in ConsumeStatement consume, in TableReference table, in IndexInfo index, out List<ColumnExpression> filter, out List<ColumnExpression> output)
        {
            table.Hints = "WITH (ROWLOCK" + (consume.StrictOrderRequired ? ")" : ", READPAST)");
            string targetName = (string.IsNullOrEmpty(table.Alias) ? string.Empty : table.Alias);

            SelectExpression select = new();

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

            //TODO: CreateConsumeOrder(in consume, in select, in output);

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
        private DeleteStatement TransformConsumeToDelete(in CommonTableExpression changes, in TableReference table, in List<ColumnExpression> index, in List<ColumnExpression> output)
        {
            DeleteStatement delete = new()
            {
                Output = new OutputClause(),
                Target = new TableReference()
                {
                    Binding = table.Binding,
                    Identifier = "target" // !?
                },
                From = new FromClause()
                {
                    Expression = new TableJoinOperator()
                    {
                        Token = TokenType.INNER,
                        Expression1 = new TableReference()
                        {
                            Alias = "target",
                            Binding = table.Binding,
                            Identifier = table.Identifier
                        },
                        Expression2 = new TableReference()
                        {
                            Binding = changes,
                            Identifier = "changes"
                        },
                        On = new OnClause()
                        {
                            Expression = CreateDeletionFilter(in index)
                        }
                    }
                }
            };

            // OUTPUT clause - CONSUME output columns

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

        public override void Visit(in CreateTypeStatement node, in StringBuilder script)
        {
            script.Append("CREATE TYPE [").Append(node.Identifier).AppendLine("] AS TABLE");
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
                        script.Append('_').Append(column.Name).Append("_L").Append(' ').Append("binary(1)");
                    }
                    else if (type == typeof(decimal)) // number(p,s)
                    {
                        script.Append('_').Append(column.Name).Append("_N")
                            .Append(' ').Append("numeric(").Append(info.Qualifier1).Append(',').Append(info.Qualifier2).Append(')');
                    }
                    else if (type == typeof(DateTime)) // datetime
                    {
                        script.Append('_').Append(column.Name).Append("_T").Append(' ').Append("datetime2");
                    }
                    else if (type == typeof(string)) // string(n)
                    {
                        script.Append('_').Append(column.Name).Append("_S").Append(' ').Append("nvarchar(")
                            .Append((info.Qualifier1 > 0) ? info.Qualifier1.ToString() : "max").Append(')');
                    }
                    else if (type == typeof(byte[])) // binary
                    {
                        script.Append('_').Append(column.Name).Append("_B").Append(' ').Append("varbinary(max)");
                    }
                    else if (type == typeof(Guid)) // uuid
                    {
                        script.Append('_').Append(column.Name).Append("_U").Append(' ').Append("binary(16)");
                    }
                    else if (type == typeof(Entity)) // entity - multiple reference type
                    {
                        script.Append('_').Append(column.Name).Append("_C").Append(' ').Append("binary(4)").AppendLine(",");
                        script.Append('_').Append(column.Name).Append("_R").Append(' ').Append("binary(16)");
                    }
                    else
                    {
                        throw new InvalidOperationException("Unsupported column data type");
                    }
                }
                else if (info.Binding is Entity entity) // single reference type, example: Справочник.Номенклатура
                {
                    script.Append('_').Append(column.Name).Append("_R_").Append(entity.TypeCode).Append(' ').Append("binary(16)");
                }
                else //TODO: union type
                {
                    throw new InvalidOperationException("Unknown column data type");
                    //script.Append('_').Append(column.Name).Append("_D").Append(' ').Append("binary(1)");
                }
            }

            script.AppendLine().AppendLine(")");
        }

        protected override void Infer(in List<ColumnMap> mapping, in List<ColumnDefinition> columns)
        {
            foreach (ColumnMap map in mapping)
            {
                ScriptHelper.GetColumnIdentifiers(map.Name, out _, out string columnName);

                if (!string.IsNullOrEmpty(map.Alias)) { columnName = map.Alias; }

                ColumnDefinition column = new()
                {
                    Name = columnName,
                    Type = new TypeIdentifier()
                    {
                        Identifier = map.TypeName
                    }
                };

                if (map.Type == UnionTag.TypeCode)
                {
                    column.Type.Identifier += "(4)";
                }
                else if (map.Type == UnionTag.Entity)
                {
                    column.Type.Identifier += "(16)";
                }
                else if (map.Type == UnionTag.String)
                {
                    column.Type.Identifier += "(1024)";
                }

                columns.Add(column);
            }
        }
    }
}

// Шаблон запроса на деструктивное чтение с обогащением данных (JOIN)
//DECLARE @result TABLE(id binary(16));
//WITH changes AS 
//(SELECT TOP (10)
//Изменения._NodeTRef AS УзелОбмена_TRef, Изменения._NodeRRef AS УзелОбмена_RRef,
//Изменения._IDRRef AS Ссылка
//FROM _ReferenceChngR1253 AS Изменения WITH (ROWLOCK, READPAST)
//ORDER BY _IDRRef DESC
//)
//DELETE target
//OUTPUT
//changes.Ссылка
//INTO @result
//FROM _ReferenceChngR1253 AS target INNER JOIN changes ON target._IDRRef = changes.Ссылка
//;
//SELECT * FROM @result ORDER BY id ASC;
//;

// Шаблон запроса на деструктивное чтение для Microsoft SQL Server
//WITH queue AS
//(SELECT TOP (@MessageCount)
//  МоментВремени, Идентификатор, ДатаВремя,
//  Отправитель, Получатели, Заголовки,
//  ТипОперации, ТипСообщения, ТелоСообщения
//FROM
//  {TABLE_NAME} WITH (ROWLOCK, READPAST)
//ORDER BY
//  МоментВремени ASC,
//  Идентификатор ASC
//)
//DELETE queue OUTPUT
//  deleted.МоментВремени, deleted.Идентификатор, deleted.ДатаВремя,
//  deleted.Отправитель, deleted.Получатели, deleted.Заголовки,
//  deleted.ТипОперации, deleted.ТипСообщения, deleted.ТелоСообщения
//;
// ??? OPTION (MAXDOP 1) ???