using DaJet.Data.Mapping;
using DaJet.Metadata.Model;
using DaJet.Metadata;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class PgSqlGenerator : ISqlGenerator
    {
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

            script.AppendLine(")");
        }

        private void VisitProjectionClause(List<SyntaxNode> projection, StringBuilder script, EntityMap mapper)
        {
            List<string> columns = new();

            foreach (SyntaxNode node in projection)
            {
                if (node is Identifier identifier)
                {
                    VisitProjectionColumn(in columns, in identifier, in mapper);
                }
                else if (node is FunctionExpression function)
                {
                    VisitProjectionFunction(in columns, in function, in mapper);
                }
                else if (node is CaseExpression expression)
                {
                    VisitProjectionCase(in columns, in expression, in mapper);
                }
            }

            script.AppendJoin("," + Environment.NewLine, columns).AppendLine();
        }
        private void VisitProjectionColumn(in List<string> columns, in Identifier identifier, in EntityMap mapper)
        {
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
                    string name = (string.IsNullOrWhiteSpace(tableAlias) ? string.Empty : tableAlias + ".");

                    name += field.Name;

                    if (mapper != null && // resulting SELECT clause only !
                        field.TypeName == "nvarchar" || field.TypeName == "varchar" ||
                        field.TypeName == "nchar" || field.TypeName == "char")
                    {
                        name = "CAST(" + name + " AS varchar)"; // text
                    }

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

                    columns.Add("\t" + name);
                }
            }
            else if (identifier.Tag is Identifier column) /// bubbled up from subquery <see cref="MetadataBinder.BindColumn"/>
            {
                string name = "\t" + (string.IsNullOrWhiteSpace(tableAlias) ? string.Empty : tableAlias + ".");

                if (string.IsNullOrWhiteSpace(column.Alias))
                {
                    name += identifier.Value;
                }
                else
                {
                    name += column.Alias;
                }

                if (!string.IsNullOrWhiteSpace(identifier.Alias))
                {
                    name += " AS " + identifier.Alias;
                }

                columns.Add(name);
            }
        }
        private void VisitProjectionFunction(in List<string> columns, in FunctionExpression function, in EntityMap mapper)
        {
            mapper
                .MapProperty(new PropertyMap()
                {
                    Name = function.Alias,
                    Type = typeof(decimal) // TODO: infer type from parameters
                })
                .ToColumn(new ColumnMap()
                {
                    Name = function.Alias
                });

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
            mapper
                .MapProperty(new PropertyMap()
                {
                    Name = expression.Alias,
                    Type = typeof(decimal) // TODO: infer type from parameters
                })
                .ToColumn(new ColumnMap()
                {
                    Name = expression.Alias
                });

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
            VisitCommonTables(select.CTE, script);

            VisitSelectClause(select, script, mapper);

            VisitFromClause(select.FROM, script);

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

            VisitTopExpression(select.TOP, script);
        }

        #region "SELECT AND FROM CLAUSE"

        private void VisitSelectClause(SelectStatement select, StringBuilder script, EntityMap mapper)
        {
            script.Append("SELECT").AppendLine();

            VisitProjectionClause(select.SELECT, script, mapper);
        }
        private void VisitTopExpression(SyntaxNode top, StringBuilder script)
        {
            if (top == null) // optional
            {
                return;
            }

            script.AppendLine().Append("LIMIT ");

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

            VisitSelectStatement(subquery.QUERY, script, null!);

            script.Append(") AS " + subquery.Alias);
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

            VisitProjectionClause(node.Expressions, script, null!);
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

        private void VisitExpression(SyntaxNode expression, StringBuilder script)
        {
            if (expression is Identifier identifier)
            {
                VisitIdentifier(identifier, script);
            }
            else if (expression is ScalarExpression scalar1)
            {
                VisitScalarExpression(scalar1, script);
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
            script.Append("-");
            VisitExpression(unary.Expression, script);
        }
        private void VisitAdditionOperator(AdditionOperator addition, StringBuilder script)
        {
            VisitExpression(addition.Expression1, script);

            if (addition.Token == TokenType.Plus)
            {
                script.Append(" + ");
            }
            else if (addition.Token == TokenType.Minus)
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

            VisitProjectionClause(function.OVER.Partition, script, null!);
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
            script.AppendLine("CASE");

            foreach (WhenExpression when in expression.CASE)
            {
                script.Append("WHEN ");
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
                    // this should be metadata binding error !
                    name = identifier.Value;
                }
            }
            else if (identifier.Tag is Identifier column) /// bubbled up from subquery <see cref="MetadataBinder.BindColumn"/>
            {
                if (string.IsNullOrWhiteSpace(column.Alias))
                {
                    VisitColumnIdentifier(column, script);
                }
                else
                {
                    name += column.Alias;
                }
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

            if (identifier.Tag is Type type && type == typeof(string))
            {
                script.Append($"CAST({name} AS mvarchar)");
            }
            else
            {
                script.Append(name);
            }
        }
        private void VisitScalarExpression(ScalarExpression scalar, StringBuilder script)
        {
            if (scalar.Literal.StartsWith("0x")) // binary literal
            {
                script.Append("'\\\\x" + scalar.Literal[2..] + "'");
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
            VisitCommonTables(delete.CTE, script);

            script.Append("DELETE ");

            VisitTableSource(delete.FROM.Expression, script);

            if (delete.OUTPUT != null) // optional
            {
                VisitOutputClause(delete.OUTPUT, script, mapper);
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