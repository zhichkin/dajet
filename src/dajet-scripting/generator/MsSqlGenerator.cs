using DaJet.Data.Mapping;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DaJet.Scripting
{
    public sealed class MsSqlGenerator : ISqlGenerator
    {
        private readonly TypeInferencer _inferencer = new();
        public int YearOffset { get; set; } = 0;
        public bool TryGenerate(in ScriptModel model, out GeneratorResult result)
        {
            result = new GeneratorResult();

            result.Mapper.YearOffset = YearOffset;

            try
            {
                StringBuilder script = new();

                foreach (SyntaxNode node in model.Statements)
                {
                    if (node is SelectStatement select)
                    {
                        VisitSelectStatement(select, script, result.Mapper);
                        script.AppendLine(";");
                    }
                    else if (node is TableUnionOperator union)
                    {
                        VisitUnionOperator(union, script, result.Mapper);
                        script.AppendLine(";");
                    }
                    else if (node is DeleteStatement delete)
                    {
                        VisitDeleteStatement(delete, script, result.Mapper);
                        script.AppendLine(";");
                    }
                }

                result.Script = script.ToString();
            }
            catch (Exception exception)
            {
                result.Error = ExceptionHelper.GetErrorMessage(exception);
            }

            result.Success = string.IsNullOrWhiteSpace(result.Error);

            return result.Success;
        }

        private void VisitCommonTables(CommonTableExpression cte, StringBuilder script)
        {
            if (cte == null)
            {
                return;
            }

            script.Append("WITH ");

            VisitCommonTable(cte, script);
        }
        private void VisitCommonTable(CommonTableExpression cte, StringBuilder script)
        {
            if (cte.Next != null)
            {
                VisitCommonTable(cte.Next, script);
            }

            if (cte.Next != null)
            {
                script.Append(", ");
            }

            script.AppendLine($"{cte.Name} AS").Append("(");

            if (cte.Expression is SelectStatement select)
            {
                VisitSelectStatement(select, script, null!);
            }
            else if (cte.Expression is TableUnionOperator union)
            {
                VisitUnionOperator(union, script, null!);
            }

            script.AppendLine(")");
        }

        private void VisitProjectionClause(List<ColumnExpression> projection, StringBuilder script, EntityMap mapper)
        {
            List<string> columns = new();

            foreach (ColumnExpression column in projection)
            {
                if (column.Expression is Identifier identifier)
                {
                    VisitProjectionColumn(in columns, in identifier, in mapper);
                }
                else if (column.Expression is FunctionExpression function)
                {
                    VisitProjectionFunction(in columns, in function, in mapper);
                }
                else if (column.Expression is CaseExpression expression)
                {
                    VisitProjectionCase(in columns, in expression, in mapper);
                }
                else
                {
                    VisitColumnExpression(in column, in columns, in mapper);
                }
            }

            script.AppendJoin("," + Environment.NewLine, columns).AppendLine();
        }
        private void VisitColumnExpression(in ColumnExpression column, in List<string> columns, in EntityMap mapper)
        {
            if (mapper != null)
            {
                mapper.MapProperty(new PropertyMap()
                {
                    Name = column.Alias,
                    Type = _inferencer.InferOrDefault(column)
                })
                    .ToColumn(new ColumnMap()
                    {
                        Name = column.Alias
                    });
            }

            StringBuilder script = new("\t");
            
            VisitExpression(column.Expression, script);

            if (!string.IsNullOrWhiteSpace(column.Alias))
            {
                script.Append(" AS ").Append(column.Alias);
            }

            columns.Add(script.ToString());
        }
        private void VisitProjectionColumn(in List<string> columns, in Identifier identifier, in EntityMap mapper)
        {
            //TODO: mapper can be null if the code is called from:
            // - VisitOrderClause
            // - VisitCommonTable
            // - VisitSubqueryExpression
            // - VisitUnionOperator
            //TODO: remove this call from VisitOrderClause procedure !!!

            ScriptHelper.GetColumnNames(identifier.Value, out string tableAlias, out string columnName);

            string propertyAlias = string.IsNullOrWhiteSpace(identifier.Alias) ? columnName : identifier.Alias;

            if (identifier.Tag is MetadataProperty property)
            {
                PropertyMap propertyMap = null!;
                if (mapper != null)
                {
                    propertyMap = DataMapper.CreatePropertyMap(in property, propertyAlias);
                    _ = mapper.MapProperty(propertyMap);
                }

                foreach (MetadataColumn field in property.Columns)
                {
                    string name = "\t" + (string.IsNullOrWhiteSpace(tableAlias) ? string.Empty : tableAlias + ".");

                    name += field.Name;

                    if (propertyMap == null) // subquery select statement
                    {

                        if (!string.IsNullOrWhiteSpace(identifier.Alias))
                        {
                            name += " AS " + identifier.Alias;

                            if (property.Columns.Count > 1)
                            {
                                name += ScriptHelper.GetColumnPurposePostfix(field.Purpose);
                            }
                        }
                    }
                    else // root select statement
                    {
                        string alias = propertyAlias;

                        if (property.Columns.Count > 1)
                        {
                            alias += ScriptHelper.GetColumnPurposePostfix(field.Purpose);
                        }

                        name += " AS " + alias;

                        ColumnMap columnMap = DataMapper.CreateColumnMap(in field, alias);
                        propertyMap.ToColumn(columnMap);
                    }

                    columns.Add(name);
                }
            }
            else if (identifier.Tag is CaseExpression expression)
            {
                if (mapper != null)
                {
                    mapper.MapProperty(new PropertyMap()
                    {
                        Name = columnName,
                        Type = _inferencer.InferOrDefault(expression)
                    })
                        .ToColumn(new ColumnMap()
                        {
                            Name = columnName
                        });
                }
                if (mapper != null)
                {
                    columns.Add("\t" + identifier.Value + " AS " + columnName);
                }
                else
                {
                    columns.Add("\t" + identifier.Value);
                }
            }
            else if (identifier.Tag is FunctionExpression function)
            {
                if (mapper != null)
                {
                    mapper.MapProperty(new PropertyMap()
                    {
                        Name = columnName,
                        Type = _inferencer.InferOrDefault(function)
                    })
                        .ToColumn(new ColumnMap()
                        {
                            Name = columnName
                        });
                }
                if (mapper != null)
                {
                    columns.Add("\t" + identifier.Value + " AS " + columnName);
                }
                else
                {
                    columns.Add("\t" + identifier.Value);
                }
            }
            else if (identifier.Tag is Identifier parent) /// bubbled up from subquery <see cref="MetadataBinder.BindColumn"/>
            {
                /// bubbled up from subquery <see cref="MetadataBinder.BindColumnToSelect(in SelectStatement, in Identifier)"/>

                //TODO: get MetadataProperty recursively, following the Tag property !!!
                if (mapper != null && parent.Tag is MetadataProperty source)
                {
                    PropertyMap propertyMap = DataMapper.CreatePropertyMap(in source, propertyAlias);
                    mapper.MapProperty(propertyMap).ToColumn(new ColumnMap()
                    {
                        Name = propertyAlias
                    });
                }

                string name = "\t" + (string.IsNullOrWhiteSpace(tableAlias) ? string.Empty : tableAlias + ".");

                if (string.IsNullOrWhiteSpace(parent.Alias))
                {
                    name += identifier.Value;
                }
                else
                {
                    name += parent.Alias;
                }

                if (!string.IsNullOrWhiteSpace(identifier.Alias))
                {
                    name += " AS " + identifier.Alias;
                }

                columns.Add(name);
            }
            else if (identifier.Tag is ColumnExpression column)
            {
                // the identifier input parameter is a derived column from the source table

                if (mapper != null)
                {
                    mapper.MapProperty(new PropertyMap()
                    {
                        Name = propertyAlias,
                        Type = _inferencer.InferOrDefault(column)
                    })
                        .ToColumn(new ColumnMap()
                        {
                            Name = propertyAlias
                        });
                }
                if (mapper != null)
                {
                    columns.Add("\t" + identifier.Value + " AS " + propertyAlias);
                }
                else
                {
                    columns.Add("\t" + identifier.Value);
                }
            }
        }
        private void VisitProjectionFunction(in List<string> columns, in FunctionExpression function, in EntityMap mapper)
        {
            if (mapper != null)
            {
                PropertyMap map = new()
                {
                    Name = function.Alias,
                    Type = _inferencer.InferOrDefault(function)
                };

                if (_inferencer.DataType is not null)
                {
                    map.TypeCode = _inferencer.DataType.TypeCode;
                }

                mapper.MapProperty(map).ToColumn(new ColumnMap()
                {
                    Name = function.Alias
                });
            }

            StringBuilder script = new("\t");

            VisitFunctionExpression(function, script);

            if (!string.IsNullOrWhiteSpace(function.Alias))
            {
                script.Append(" AS ").Append(function.Alias);
            }

            columns.Add(script.ToString());
        }
        private void VisitProjectionCase(in List<string> columns, in CaseExpression expression, in EntityMap mapper)
        {
            if (mapper != null)
            {
                PropertyMap map = new()
                {
                    Name = expression.Alias,
                    Type = _inferencer.InferOrDefault(expression)
                };

                if (_inferencer.DataType is not null)
                {
                    map.TypeCode = _inferencer.DataType.TypeCode;
                }

                mapper.MapProperty(map).ToColumn(new ColumnMap()
                {
                    Name = expression.Alias
                });
            }

            StringBuilder script = new("\t");

            VisitCaseExpression(expression, script);

            if (!string.IsNullOrWhiteSpace(expression.Alias))
            {
                script.Append(" AS ").Append(expression.Alias);
            }

            columns.Add(script.ToString());
        }

        #region "SELECT STATEMENT"
        private void VisitSelectStatement(SelectStatement select, StringBuilder script, EntityMap mapper)
        {
            if (select.IsExpression) { script.Append("("); } // can be used by UNION operator

            VisitCommonTables(select.CommonTables, script);

            VisitSelectClause(select, script, mapper);

            if (select.FROM != null) // optional
            {
                VisitFromClause(select.FROM, script);
            }

            if (select.WHERE != null) // optional
            {
                VisitWhereClause(select.WHERE, script);
            }

            if (select.GROUP != null) // optional
            {
                VisitGroupClause(select.GROUP, script);
            }

            if (select.HAVING != null) // optional
            {
                VisitHavingClause(select.HAVING, script);
            }

            if (select.ORDER != null) // optional
            {
                VisitOrderClause(select.ORDER, script);
            }

            if (select.IsExpression) { script.Append(")"); } // can be used by UNION operator
        }

        #region "SELECT AND FROM CLAUSE"
        private void VisitSelectClause(SelectStatement select, StringBuilder script, EntityMap mapper)
        {
            script.Append("SELECT");

            VisitTopExpression(select.TOP, script);

            script.AppendLine();

            VisitProjectionClause(select.SELECT, script, mapper);
        }
        private void VisitTopExpression(SyntaxNode top, StringBuilder script)
        {
            if (top == null) // optional
            {
                return;
            }

            script.Append(" TOP ");

            if (top is ScalarExpression scalar && scalar.Token == TokenType.Number)
            {
                VisitScalarExpression(scalar, script);
            }
            else if (top is Identifier variable && variable.Token == TokenType.Variable)
            {
                script.Append("(");
                VisitIdentifier(variable, script);
                script.Append(")");
            }
        }
        private void VisitFromClause(FromClause from, StringBuilder script)
        {
            script.Append("FROM ");

            VisitTableSource(from.Expression, script);
        }
        private void VisitTableSource(SyntaxNode node, StringBuilder script)
        {
            if (node is TableJoinOperator join)
            {
                VisitJoinOperator(join, script);
            }
            else if (node is TableSource table)
            {
                if (table.Expression is Identifier identifier && identifier.Token == TokenType.Table)
                {
                    VisitTableIdentifier(table, identifier, script);
                }
                else if (table.Expression is SubqueryExpression subquery)
                {
                    VisitSubqueryExpression(subquery, script);
                }
            }
        }
        private void VisitOrderClause(OrderClause order, StringBuilder script)
        {
            script.AppendLine().AppendLine("ORDER BY");

            List<string> result = new();
            List<string> columns = new();

            foreach (OrderExpression expression in order.Expressions)
            {
                if (expression.Expression is not Identifier identifier)
                {
                    continue;
                }

                columns.Clear();

                VisitProjectionColumn(in columns, in identifier, null!);

                for (int i = 0; i < columns.Count; i++)
                {
                    if (expression.Token == TokenType.DESC)
                    {
                        result.Add(columns[i] + " DESC");
                    }
                    else
                    {
                        result.Add(columns[i] + " ASC");
                    }
                }
            }

            script.AppendJoin("," + Environment.NewLine, result).AppendLine();

            if (order.Offset != null) // optional
            {
                script.Append("OFFSET ");
                VisitExpression(order.Offset, script);
                script.AppendLine(" ROWS");

                if (order.Fetch != null)
                {
                    script.Append("FETCH NEXT ");
                    VisitExpression(order.Fetch, script);
                    script.AppendLine(" ROWS ONLY");
                }
            }
        }

        private void VisitTableIdentifier(TableSource table, Identifier identifier, StringBuilder script)
        {
            string tableName = string.Empty;

            if (identifier.Tag is ApplicationObject entity)
            {
                tableName = entity.TableName;
            }
            else if (identifier.Tag is CommonTableExpression cte)
            {
                tableName = cte.Name;
            }

            if (string.IsNullOrWhiteSpace(tableName))
            {
                return;
            }

            script.Append(tableName);

            if (!string.IsNullOrWhiteSpace(identifier.Alias))
            {
                script.Append(" AS ").Append(identifier.Alias);
            }

            if (table.Hints.Count > 0)
            {
                script.Append(" WITH (");

                StringBuilder hints = new();

                foreach (TokenType hint in table.Hints)
                {
                    if (hints.Length > 0)
                    {
                        hints.Append(", ");
                    }
                    hints.Append(hint.ToString());
                }

                script.Append(hints.ToString()).Append(")");
            }
        }
        private void VisitJoinOperator(TableJoinOperator join, StringBuilder script)
        {
            VisitTableSource(join.Expression1, script);

            script.AppendLine().Append(join.Token.ToString()).Append(" JOIN ");

            VisitTableSource(join.Expression2, script);

            VisitOnClause(join.ON, script);
        }
        private void VisitSubqueryExpression(SubqueryExpression subquery, StringBuilder script)
        {
            script.Append("(");

            if (subquery.Expression is SelectStatement select)
            {
                VisitSelectStatement(select, script, null!);
            }
            else if (subquery.Expression is TableUnionOperator union)
            {
                VisitUnionOperator(union, script, null!);
            }

            script.Append(") AS " + subquery.Alias);
        }
        private void VisitUnionOperator(TableUnionOperator union, StringBuilder script, EntityMap mapper)
        {
            if (union.Expression1 is SelectStatement select1)
            {
                VisitSelectStatement(select1, script, mapper);
            }
            else if (union.Expression1 is TableUnionOperator union1)
            {
                VisitUnionOperator(union1, script, null!);
            }

            if (union.Token == TokenType.UNION)
            {
                script.AppendLine().AppendLine("UNION");
            }
            else
            {
                script.AppendLine().AppendLine("UNION ALL");
            }

            if (union.Expression2 is SelectStatement select2)
            {
                VisitSelectStatement(select2, script, null!);
            }
            else if (union.Expression2 is TableUnionOperator union2)
            {
                VisitUnionOperator(union2, script, null!);
            }
        }
        #endregion

        #region "WHERE AND ON CLAUSE"

        private void VisitOnClause(OnClause node, StringBuilder script)
        {
            script.AppendLine().Append("ON ");

            VisitBooleanExpression(node.Expression, script);
        }
        private void VisitWhereClause(WhereClause node, StringBuilder script)
        {
            script.AppendLine().Append("WHERE ");

            VisitBooleanExpression(node.Expression, script);
        }
        private void VisitGroupClause(GroupClause node, StringBuilder script)
        {
            script.AppendLine().AppendLine("GROUP BY");

            VisitExpressions(node.Expressions, script);
        }
        private void VisitHavingClause(HavingClause node, StringBuilder script)
        {
            script.Append("HAVING ");

            VisitBooleanExpression(node.Expression, script);
        }

        private void VisitBooleanExpression(SyntaxNode node, StringBuilder script)
        {
            if (node is ComparisonOperator comparison)
            {
                VisitComparisonOperator(comparison, script);
            }
            else if (node is BooleanUnaryOperator unary)
            {
                VisitBooleanUnaryOperator(unary, script);
            }
            else if (node is BooleanBinaryOperator binary)
            {
                VisitBooleanBinaryOperator(binary, script);
            }
            else if (node is BooleanGroupExpression group)
            {
                script.Append("(");
                VisitExpression(group.Expression, script); // VisitBooleanExpression(group.Expression, script);
                script.Append(")");
            }
            else if (node is CaseExpression _case)
            {
                VisitExpression(_case, script);
            }
        }
        private void VisitBooleanUnaryOperator(BooleanUnaryOperator node, StringBuilder script)
        {
            script.Append("NOT ");

            VisitBooleanExpression(node.Expression, script);
        }
        private void VisitBooleanBinaryOperator(BooleanBinaryOperator node, StringBuilder script)
        {
            VisitBooleanExpression(node.Expression1, script);

            script.AppendLine().Append(node.Token.ToString()).Append(" ");

            VisitBooleanExpression(node.Expression2, script);
        }
        private void VisitComparisonOperator(ComparisonOperator node, StringBuilder script)
        {
            VisitExpression(node.Expression1, script);

            script.Append(" ").Append(ScriptHelper.GetComparisonLiteral(node.Token)).Append(" ");

            VisitExpression(node.Expression2, script);
        }

        private void SeparateExpression(StringBuilder script, in string separator, ref bool first)
        {
            if (first)
            {
                first = false;
            }
            else
            {
                script.Append(separator);
            }
        }
        private void VisitExpressions(List<SyntaxNode> expressions, StringBuilder script)
        {
            bool first = true;
            string separator = "," + Environment.NewLine;

            foreach (SyntaxNode node in expressions)
            {
                SeparateExpression(script, in separator, ref first);

                if (node is Identifier identifier)
                {
                    VisitIdentifier(identifier, script);
                }
                else if (node is FunctionExpression function)
                {
                    VisitFunctionExpression(function, script);
                }
                else if (node is CaseExpression expression)
                {
                    VisitCaseExpression(expression, script);
                }
            }

            script.AppendLine();
        }
        private void VisitExpression(SyntaxNode expression, StringBuilder script)
        {
            if (expression is Identifier identifier)
            {
                VisitIdentifier(identifier, script);
            }
            else if (expression is ScalarExpression scalar)
            {
                VisitScalarExpression(scalar, script);
            }
            else if (expression is UnaryOperator unary)
            {
                VisitUnaryOperator(unary, script);
            }
            else if (expression is AdditionOperator addition)
            {
                VisitAdditionOperator(addition, script);
            }
            else if (expression is MultiplyOperator multiply)
            {
                VisitMultiplyOperator(multiply, script);
            }
            else if (expression is FunctionExpression function)
            {
                VisitFunctionExpression(function, script);
            }
            else if (expression is CaseExpression case_when)
            {
                VisitCaseExpression(case_when, script);
            }
            else
            {
                VisitBooleanExpression(expression, script);
            }
        }
        private void VisitUnaryOperator(UnaryOperator unary, StringBuilder script)
        {
            script.Append(unary.Token == TokenType.Minus ? "-" : "NOT ");
            VisitExpression(unary.Expression, script);
        }
        private void VisitAdditionOperator(AdditionOperator addition, StringBuilder script)
        {
            VisitExpression(addition.Expression1, script);

            if(addition.Token == TokenType.Plus)
            {
                script.Append(" + ");
            }
            else if(addition.Token == TokenType.Minus)
            {
                script.Append(" - ");
            }

            VisitExpression(addition.Expression2, script);
        }
        private void VisitMultiplyOperator(MultiplyOperator multiply, StringBuilder script)
        {
            VisitExpression(multiply.Expression1, script);

            if (multiply.Token == TokenType.Star)
            {
                script.Append(" * ");
            }
            else if (multiply.Token == TokenType.Divide)
            {
                script.Append(" / ");
            }
            else if (multiply.Token == TokenType.Modulo)
            {
                script.Append(" % ");
            }

            VisitExpression(multiply.Expression2, script);
        }
        
        private void VisitFunctionExpression(FunctionExpression function, StringBuilder script)
        {
            script.Append(function.Name);

            script.Append("(");

            for (int i = 0; i < function.Parameters.Count; i++)
            {
                SyntaxNode expression = function.Parameters[i];

                if (i > 0)
                {
                    script.Append(", ");
                }

                VisitExpression(expression, script);
            }

            script.Append(")");

            if (function.OVER != null)
            {
                script.Append(" ");
                VisitOverClause(function, script);
            }
        }
        private void VisitOverClause(FunctionExpression function, StringBuilder script)
        {
            script.Append("OVER");
            script.Append("(");

            if (function.OVER.Partition.Count > 0)
            {
                VisitPartitionClause(function, script);
            }

            if (function.OVER.Order != null)
            {
                VisitOrderClause(function.OVER.Order, script);
            }

            if (function.OVER.Preceding != null || function.OVER.Following != null)
            {
                script.Append(function.OVER.FrameType.ToString()).Append(" ");

                if (function.OVER.Preceding != null && function.OVER.Following != null)
                {
                    script.Append("BETWEEN").Append(" ");

                    VisitWindowFrame(function.OVER.Preceding, script);

                    script.Append(" AND ");

                    VisitWindowFrame(function.OVER.Following, script);
                }
                else if (function.OVER.Preceding != null)
                {
                    VisitWindowFrame(function.OVER.Preceding, script);
                }
            }
            
            script.Append(")");
        }
        private void VisitPartitionClause(FunctionExpression function, StringBuilder script)
        {
            script.AppendLine().AppendLine("PARTITION BY");

            VisitExpressions(function.OVER.Partition, script);
        }
        private void VisitWindowFrame(WindowFrame frame, StringBuilder script)
        {
            if (frame.Extent == -1)
            {
                script.Append("UNBOUNDED ").Append(frame.Token.ToString());
            }
            else if (frame.Extent == 0)
            {
                script.Append("CURRENT ROW");
            }
            else if (frame.Extent > 0)
            {
                script
                    .Append(frame.Extent.ToString())
                    .Append(" ")
                    .Append(frame.Token.ToString());
            }
        }

        private void VisitCaseExpression(CaseExpression expression, StringBuilder script)
        {
            script.Append("CASE");

            foreach (WhenExpression when in expression.CASE)
            {
                script.Append(" WHEN ");
                VisitExpression(when.WHEN, script);
                script.Append(" THEN ");
                VisitExpression(when.THEN, script);
            }

            if (expression.ELSE != null)
            {
                script.Append(" ELSE ");
                VisitExpression(expression.ELSE, script);
            }

            script.Append(" END");
        }
        private void VisitIdentifier(Identifier identifier, StringBuilder script)
        {
            if (identifier.Token == TokenType.Column)
            {
                VisitColumnIdentifier(identifier, script);
            }
            else if (identifier.Token == TokenType.Variable)
            {
                VisitVariableIdentifier(identifier, script);
            }
        }
        private void VisitColumnIdentifier(Identifier identifier, StringBuilder script)
        {
            ScriptHelper.GetColumnNames(identifier.Value, out string tableAlias, out string columnName);

            string name = string.IsNullOrWhiteSpace(tableAlias) ? string.Empty : tableAlias + ".";

            if (identifier.Tag is MetadataColumn field) // union type properties
            {
                name += field.Name;
            }
            else if (identifier.Tag is MetadataProperty property) // single type properties
            {
                if (property.Columns.Count == 1)
                {
                    name += property.Columns[0].Name;
                }
                else
                {
                    //TODO: this should be metadata binding error !
                    name = identifier.Value;
                }
            }
            else if (identifier.Tag is CaseExpression)
            {
                name = identifier.Value;
            }
            else if (identifier.Tag is FunctionExpression)
            {
                name = identifier.Value;
            }
            else if (identifier.Tag is ScalarExpression)
            {
                name = identifier.Value;
            }
            else if (identifier.Tag is Identifier parent) /// bubbled up from subquery <see cref="MetadataBinder.BindColumn"/>
            {
                if (string.IsNullOrWhiteSpace(parent.Alias))
                {
                    VisitColumnIdentifier(parent, script);
                }
                else
                {
                    name += parent.Alias;
                }
            }
            else if (identifier.Tag is ColumnExpression column)
            {
                // the identifier input parameter is a derived column from the source table
                name = identifier.Value;
            }

            script.Append(name);
        }
        private void VisitVariableIdentifier(Identifier identifier, StringBuilder script)
        {
            string name = identifier.Value;

            if (identifier.Value.StartsWith('&'))
            {
                name = name.Replace('&', '@');
            }

            script.Append(name);
        }
        private void VisitScalarExpression(ScalarExpression scalar, StringBuilder script)
        {
            if (scalar.Token == TokenType.Boolean)
            {
                if (ScriptHelper.IsTrueLiteral(scalar.Literal))
                {
                    script.Append("0x01");
                }
                else
                {
                    script.Append("0x00");
                }
            }
            else
            {
                script.Append(scalar.Literal);
            }
        }

        #endregion

        #endregion

        #region "DELETE STATEMENT"

        private void VisitDeleteStatement(DeleteStatement delete, StringBuilder script, EntityMap mapper)
        {
            VisitCommonTables(delete.CommonTables, script);

            script.Append("DELETE ");

            VisitTableSource(delete.TARGET, script);

            if (delete.OUTPUT != null) // optional
            {
                VisitOutputClause(delete.OUTPUT, script, mapper);
            }

            if (delete.FROM != null) // optional
            {
                VisitFromClause(delete.FROM, script);
            }

            if (delete.WHERE != null) // optional
            {
                VisitWhereClause(delete.WHERE, script);
            }
        }
        private void VisitOutputClause(OutputClause output, StringBuilder script, EntityMap mapper)
        {
            script.AppendLine().AppendLine("OUTPUT");

            VisitProjectionClause(output.Expressions, script, mapper);
        }

        #endregion
    }
}