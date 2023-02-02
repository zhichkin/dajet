using DaJet.Data.Mapping;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    public abstract class SqlGenerator : ISqlGenerator
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
                        Visit(in select, in script);
                        ConfigureDataMapper(in select, result.Mapper);
                    }
                    else if (node is DeleteStatement delete)
                    {
                        //TODO: VisitDeleteStatement(delete, script, result.Mapper);
                    }
                    script.AppendLine(";");
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

        private void ConfigureDataMapper(in SelectStatement statement, in EntityMap mapper)
        {
            // projection
        }

        private void Visit(in SelectStatement statement, in StringBuilder script)
        {
            if (statement.CommonTables is not null)
            {
                script.Append("WITH ");
                Visit(statement.CommonTables, in script);
            }
            Visit(statement.Select, in script);
        }

        private void Visit(in SyntaxNode expression, in StringBuilder script)
        {
            if (expression is GroupOperator group)
            {
                Visit(in group, in script);
            }
            else if (expression is UnaryOperator unary)
            {
                Visit(in unary, in script);
            }
            else if (expression is BinaryOperator binary)
            {
                Visit(in binary, in script);
            }
            else if (expression is AdditionOperator addition)
            {
                Visit(in addition, in script);
            }
            else if (expression is MultiplyOperator multiply)
            {
                Visit(in multiply, in script);
            }
            else if (expression is ComparisonOperator comparison)
            {
                Visit(in comparison, in script);
            }
            else if (expression is CaseExpression case_when)
            {
                Visit(in case_when, in script);
            }
            else if (expression is ScalarExpression scalar)
            {
                Visit(in scalar, in script);
            }
            else if (expression is VariableReference variable)
            {
                Visit(in variable, in script);
            }
            else if (expression is SelectExpression select)
            {
                Visit(in select, in script);
            }
            else if (expression is TableJoinOperator join)
            {
                Visit(in join, in script);
            }
            else if (expression is TableUnionOperator union)
            {
                Visit(in union, in script);
            }
            else if (expression is TableExpression derived)
            {
                Visit(in derived, in script);
            }
            else if (expression is TableReference table)
            {
                Visit(in table, in script);
            }
            else if (expression is ColumnReference column)
            {
                Visit(in column, in script);
            }
            else if (expression is FunctionExpression function)
            {
                Visit(in function, in script);
            }
        }
        private void Visit(in ColumnExpression node, in StringBuilder script)
        {
            Visit(node.Expression, in script);

            if (!string.IsNullOrEmpty(node.Alias))
            {
                script.Append(" AS ").Append(node.Alias);
            }
        }
        private void Visit(in TableReference node, in StringBuilder script)
        {
            if (node.Tag is ApplicationObject entity)
            {
                script.Append(entity.TableName);
            }
            else if (node.Tag is TableExpression table)
            {
                script.Append(table.Alias);
            }
            else if (node.Tag is CommonTableExpression cte)
            {
                script.Append(cte.Name);
            }

            if (!string.IsNullOrWhiteSpace(node.Alias))
            {
                script.Append(" AS ").Append(node.Alias);
            }

            //if (table.Hints.Count > 0)
            //{
            //    script.Append(" WITH (");

            //    StringBuilder hints = new();

            //    foreach (TokenType hint in table.Hints)
            //    {
            //        if (hints.Length > 0)
            //        {
            //            hints.Append(", ");
            //        }
            //        hints.Append(hint.ToString());
            //    }

            //    script.Append(hints.ToString()).Append(")");
            //}
        }
        private void Visit(in ColumnReference node, in StringBuilder script)
        {
            if (node.Tag is MetadataColumn column)
            {
                script.Append(column.Name);
            }
            else if (node.Tag is MetadataProperty property)
            {
                script.Append(property.Columns[0].Name);
            }
            else if (node.Tag is ColumnExpression parent)
            {
                script.Append(node.Identifier);
            }
        }
        private void Visit(in TableExpression table, in StringBuilder script)
        {
            script.Append("(");
            Visit(table.Expression, in script);
            script.Append(") AS " + table.Alias);
        }
        private void Visit(in TableJoinOperator join, in StringBuilder script)
        {
            Visit(join.Expression1, in script);
            script.AppendLine().Append(join.Token.ToString()).Append(" JOIN ");
            Visit(join.Expression2, in script);
            Visit(join.On, in script);
        }
        private void Visit(in TableUnionOperator union, in StringBuilder script)
        {
            if (union.Expression1 is SelectExpression select1)
            {
                Visit(in select1, in script);
            }
            else if (union.Expression1 is TableUnionOperator union1)
            {
                Visit(in union1, in script);
            }
            if (union.Token == TokenType.UNION)
            {
                script.AppendLine().AppendLine("UNION");
            }
            else
            {
                script.AppendLine().AppendLine("UNION ALL");
            }
            if (union.Expression2 is SelectExpression select2)
            {
                Visit(in select2, in script);
            }
            else if (union.Expression2 is TableUnionOperator union2)
            {
                Visit(in union2, in script);
            }
        }
        private void Visit(in CommonTableExpression cte, in StringBuilder script)
        {
            if (cte.Next is not null)
            {
                Visit(cte.Next, in script);
            }

            if (cte.Next is not null) { script.Append(", "); }
            script.AppendLine($"{cte.Name} AS ").Append("(");
            Visit(cte.Expression, in script);
            script.AppendLine(")");
        }
        private void Visit(in SelectExpression select, in StringBuilder script)
        {
            script.Append("SELECT");

            if (select.Top is not null)
            {
                Visit(select.Top, in script);
            }
            script.AppendLine();

            for (int i = 0; i < select.Select.Count; i++)
            {
                if (i > 0) { script.Append(", "); }
                Visit(select.Select[i], in script);
            }

            if (select.From is not null) { Visit(select.From, in script); }
            if (select.Where is not null) { Visit(select.Where, in script); }
            if (select.Group is not null) { Visit(select.Group, in script); }
            if (select.Having is not null) { Visit(select.Having, in script); }
            if (select.Order is not null) { Visit(select.Order, in script); }
        }
        private void Visit(in TopClause node, in StringBuilder script)
        {
            script.Append(" TOP ").Append("(");
            Visit(node.Expression, in script);
            script.Append(")");
        }
        private void Visit(in FromClause node, in StringBuilder script)
        {
            script.Append("FROM ");
            Visit(node.Expression, in script);
        }
        private void Visit(in WhereClause node, in StringBuilder script)
        {
            script.AppendLine().Append("WHERE ");
            Visit(node.Expression, in script);
        }
        private void Visit(in GroupClause node, in StringBuilder script)
        {
            if (node is null || node.Expressions is null || node.Expressions.Count == 0)
            {
                return;
            }

            script.AppendLine().AppendLine("GROUP BY");

            string separator = "," + Environment.NewLine;
            
            for (int i = 0; i < node.Expressions.Count; i++)
            {
                if (i > 0) { script.Append(separator); }
                Visit(node.Expressions[i], in script);
            }
            script.AppendLine();
        }
        private void Visit(in HavingClause node, in StringBuilder script)
        {
            script.Append("HAVING ");
            Visit(node.Expression, in script);
        }
        private void Visit(in OnClause node, in StringBuilder script)
        {
            script.AppendLine().Append("ON ");
            Visit(node.Expression, in script);
        }
        private void Visit(in OrderClause node, in StringBuilder script)
        {
            script.AppendLine().AppendLine("ORDER BY");

            OrderExpression order;

            string separator = ", ";

            for (int i = 0; i < node.Expressions.Count; i++)
            {
                order = node.Expressions[i];

                if (i > 0) { script.Append(separator); }

                Visit(order.Expression, in script);

                if (order.Token == TokenType.DESC)
                {
                    script.Append(" DESC");
                }
                else
                {
                    script.Append(" ASC"); // default
                }
            }

            script.AppendLine();

            if (node.Offset is not null)
            {
                script.Append("OFFSET ");
                Visit(node.Offset, in script);
                script.AppendLine(" ROWS");

                if (node.Fetch is not null)
                {
                    script.Append("FETCH NEXT ");
                    Visit(node.Fetch, in script);
                    script.AppendLine(" ROWS ONLY");
                }
            }
        }
        private void Visit(in GroupOperator node, in StringBuilder script)
        {
            script.Append("(");
            Visit(node.Expression, in script);
            script.Append(")");
        }
        private void Visit(in UnaryOperator node, in StringBuilder script)
        {
            script.Append(node.Token == TokenType.Minus ? "-" : "NOT ");
            Visit(node.Expression, in script);
        }
        private void Visit(in BinaryOperator node, in StringBuilder script)
        {
            Visit(node.Expression1, in script);
            script.AppendLine().Append(node.Token.ToString()).Append(" ");
            Visit(node.Expression2, in script);
        }
        private void Visit(in AdditionOperator node, in StringBuilder script)
        {
            Visit(node.Expression1, in script);
            if (node.Token == TokenType.Plus)
            {
                script.Append(" + ");
            }
            else if (node.Token == TokenType.Minus)
            {
                script.Append(" - ");
            }
            Visit(node.Expression2, in script);
        }
        private void Visit(in MultiplyOperator node, in StringBuilder script)
        {
            Visit(node.Expression1, in script);
            if (node.Token == TokenType.Star)
            {
                script.Append(" * ");
            }
            else if (node.Token == TokenType.Divide)
            {
                script.Append(" / ");
            }
            else if (node.Token == TokenType.Modulo)
            {
                script.Append(" % ");
            }
            Visit(node.Expression2, in script);
        }
        private void Visit(in ComparisonOperator node, in StringBuilder script)
        {
            Visit(node.Expression1, in script);
            script.Append(" ").Append(ScriptHelper.GetComparisonLiteral(node.Token)).Append(" ");
            Visit(node.Expression2, in script);
        }
        private void Visit(in CaseExpression node, in StringBuilder script)
        {
            script.Append("CASE");
            foreach (WhenExpression when in node.CASE)
            {
                script.Append(" WHEN ");
                Visit(when.WHEN, in script);
                script.Append(" THEN ");
                Visit(when.THEN, in script);
            }
            if (node.ELSE is not null)
            {
                script.Append(" ELSE ");
                Visit(node.ELSE, in script);
            }
            script.Append(" END");
        }
        private void Visit(in ScalarExpression node, in StringBuilder script)
        {
            if (node.Token == TokenType.Boolean)
            {
                if (ScriptHelper.IsTrueLiteral(node.Literal))
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
                script.Append(node.Literal);
            }
        }
        private void Visit(in VariableReference node, in StringBuilder script)
        {
            string name = node.Identifier;
            
            if (node.Identifier.StartsWith('&'))
            {
                name = name.Replace('&', '@');
            }
            
            script.Append(name);
        }

        private void Visit(in FunctionExpression function, in StringBuilder script)
        {
            script.Append(function.Name).Append("(");

            SyntaxNode expression;

            for (int i = 0; i < function.Parameters.Count; i++)
            {
                expression = function.Parameters[i];
                if (i > 0) { script.Append(", "); }
                Visit(in expression, in script);
            }

            script.Append(")");

            if (function.Over is not null)
            {
                script.Append(" ");
                VisitOverClause(in function, in script);
            }
        }
        private void VisitOverClause(in FunctionExpression function, in StringBuilder script)
        {
            script.Append("OVER").Append("(");
            if (function.Over.Partition.Count > 0)
            {
                VisitPartitionClause(in function, in script);
            }
            if (function.Over.Order is not null)
            {
                Visit(function.Over.Order, in script);
            }
            if (function.Over.Preceding is not null || function.Over.Following is not null)
            {
                script.Append(function.Over.FrameType.ToString()).Append(" ");

                if (function.Over.Preceding is not null && function.Over.Following is not null)
                {
                    script.Append("BETWEEN").Append(" ");

                    VisitWindowFrame(function.Over.Preceding, in script);

                    script.Append(" AND ");

                    VisitWindowFrame(function.Over.Following, in script);
                }
                else if (function.Over.Preceding is not null)
                {
                    VisitWindowFrame(function.Over.Preceding, in script);
                }
            }
            script.Append(")");
        }
        private void VisitPartitionClause(in FunctionExpression function, in StringBuilder script)
        {
            script.AppendLine().AppendLine("PARTITION BY");

            SyntaxNode expression;

            for (int i = 0; i < function.Over.Partition.Count; i++)
            {
                expression = function.Over.Partition[i];
                if (i > 0) { script.Append(", "); }
                Visit(in expression, in script);
            }
        }
        private void VisitWindowFrame(in WindowFrame frame, in StringBuilder script)
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

        #region "DELETE STATEMENT"
        private void VisitDeleteStatement(DeleteStatement delete, StringBuilder script, EntityMap mapper)
        {
            Visit(delete.CommonTables, script);

            script.Append("DELETE ");

            Visit(delete.TARGET, script);

            if (delete.OUTPUT != null) // optional
            {
                VisitOutputClause(delete.OUTPUT, script, mapper);
            }

            if (delete.FROM != null) // optional
            {
                Visit(delete.FROM, script);
            }

            if (delete.WHERE != null) // optional
            {
                Visit(delete.WHERE, script);
            }
        }
        private void VisitOutputClause(OutputClause output, StringBuilder script, EntityMap mapper)
        {
            script.AppendLine().AppendLine("OUTPUT");

            for (int i = 0; i < output.Expressions.Count; i++)
            {
                if (i > 0) { script.Append(", "); }

                Visit(output.Expressions[i], in script);
            }
        }
        #endregion
    }
}