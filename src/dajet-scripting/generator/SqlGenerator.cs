using DaJet.Data.Mapping;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    public abstract class SqlGenerator : ISqlGenerator
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
                        Visit(in select, in script);
                        script.Append(";");

                        ConfigureDataMapper(in select, result.Mapper);
                    }
                    else if (node is DeleteStatement delete)
                    {
                        //TODO: VisitDeleteStatement(delete, script, result.Mapper);
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
        private void ConfigureDataMapper(in SelectStatement statement, in EntityMap mapper)
        {
            if (statement.Select is SelectExpression projection)
            {
                foreach (ColumnExpression column in projection.Select)
                {
                    DataMapper.Map(in column, in mapper);
                }
            }
            else if (statement.Select is TableUnionOperator union)
            {
                //TODO: select = union.Expression1/2 as SelectStatement;
            }
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
        protected virtual void Visit(in SelectStatement node, in StringBuilder script)
        {
            if (node.CommonTables is not null)
            {
                script.Append("WITH ");
                Visit(node.CommonTables, in script);
            }
            Visit(node.Select, in script);
        }
        protected virtual void Visit(in TableReference node, in StringBuilder script)
        {
            if (node.Tag is ApplicationObject entity)
            {
                script.Append(entity.TableName);
            }
            else if (node.Tag is TableExpression || node.Tag is CommonTableExpression)
            {
                script.Append(node.Identifier);
            }

            if (!string.IsNullOrEmpty(node.Alias))
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
        protected virtual void Visit(in ColumnReference node, in StringBuilder script)
        {
            ScriptHelper.GetColumnIdentifiers(node.Identifier, out string tableAlias, out string columnName);

            if (!string.IsNullOrEmpty(tableAlias))
            {
                script.Append($"{tableAlias}.");
            }

            if (node.Tag is MetadataProperty source)
            {
                Visit(in source, in script);
            }
            else if (node.Tag is ColumnExpression parent)
            {
                Visit(in parent, in script);

                if (!string.IsNullOrEmpty(parent.Alias))
                {
                    script.Append(" AS ").Append(parent.Alias);
                }
            }
            //else if (node.Tag is MetadataColumn column)
            //{
            //    //TODO: script.Append(column.Name);
            //}
        }
        protected virtual void Visit(in MetadataProperty property, in StringBuilder script)
        {
            if (property.Columns.Count == 1)
            {
                script.Append(property.Columns[0].Name);
            }
            else
            {
                List<MetadataColumn> columns = property.Columns
                    .OrderBy((column) => { return column.Purpose; })
                    .ToList();

                for (int i = 0; i < columns.Count; i++)
                {
                    MetadataColumn column = columns[i];
                    if (i > 0) { script.Append(", "); }
                    script.Append(column.Name);
                }
            }
        }
        protected virtual void Visit(in ColumnExpression node, in StringBuilder script)
        {
            Visit(node.Expression, in script);
        }
        protected virtual void Visit(in TableExpression node, in StringBuilder script)
        {
            script.Append("(");
            Visit(node.Expression, in script);
            script.Append(") AS " + node.Alias);
        }
        protected virtual void Visit(in TableJoinOperator node, in StringBuilder script)
        {
            Visit(node.Expression1, in script);
            script.AppendLine().Append(node.Token.ToString()).Append(" JOIN ");
            Visit(node.Expression2, in script);
            Visit(node.On, in script);
        }
        protected virtual void Visit(in TableUnionOperator node, in StringBuilder script)
        {
            if (node.Expression1 is SelectExpression select1)
            {
                Visit(in select1, in script);
            }
            else if (node.Expression1 is TableUnionOperator union1)
            {
                Visit(in union1, in script);
            }
            if (node.Token == TokenType.UNION)
            {
                script.AppendLine().AppendLine("UNION");
            }
            else
            {
                script.AppendLine().AppendLine("UNION ALL");
            }
            if (node.Expression2 is SelectExpression select2)
            {
                Visit(in select2, in script);
            }
            else if (node.Expression2 is TableUnionOperator union2)
            {
                Visit(in union2, in script);
            }
        }
        protected virtual void Visit(in CommonTableExpression node, in StringBuilder script)
        {
            if (node.Next is not null)
            {
                Visit(node.Next, in script);
            }
            if (node.Next is not null) { script.Append(", "); }
            script.AppendLine($"{node.Name} AS ").Append("(");
            Visit(node.Expression, in script);
            script.AppendLine(")");
        }
        protected virtual void Visit(in SelectExpression node, in StringBuilder script)
        {
            script.Append("SELECT");

            if (node.Top is not null)
            {
                Visit(node.Top, in script);
            }
            script.AppendLine();

            for (int i = 0; i < node.Select.Count; i++)
            {
                if (i > 0) { script.Append("," + Environment.NewLine); }

                Visit(node.Select[i], in script);
            }

            if (node.From is not null) { Visit(node.From, in script); }
            if (node.Where is not null) { Visit(node.Where, in script); }
            if (node.Group is not null) { Visit(node.Group, in script); }
            if (node.Having is not null) { Visit(node.Having, in script); }
            if (node.Order is not null) { Visit(node.Order, in script); }
        }
        protected virtual void Visit(in TopClause node, in StringBuilder script)
        {
            script.Append(" TOP ").Append("(");
            Visit(node.Expression, in script);
            script.Append(")");
        }
        protected virtual void Visit(in FromClause node, in StringBuilder script)
        {
            script.AppendLine().Append("FROM ");
            Visit(node.Expression, in script);
        }
        protected virtual void Visit(in WhereClause node, in StringBuilder script)
        {
            script.AppendLine().Append("WHERE ");
            Visit(node.Expression, in script);
        }
        protected virtual void Visit(in GroupClause node, in StringBuilder script)
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
        protected virtual void Visit(in HavingClause node, in StringBuilder script)
        {
            script.Append("HAVING ");
            Visit(node.Expression, in script);
        }
        protected virtual void Visit(in OnClause node, in StringBuilder script)
        {
            script.AppendLine().Append("ON ");
            Visit(node.Expression, in script);
        }
        protected virtual void Visit(in OrderClause node, in StringBuilder script)
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
        protected virtual void Visit(in GroupOperator node, in StringBuilder script)
        {
            script.Append("(");
            Visit(node.Expression, in script);
            script.Append(")");
        }
        protected virtual void Visit(in UnaryOperator node, in StringBuilder script)
        {
            script.Append(node.Token == TokenType.Minus ? "-" : "NOT ");
            Visit(node.Expression, in script);
        }
        protected virtual void Visit(in BinaryOperator node, in StringBuilder script)
        {
            Visit(node.Expression1, in script);
            script.AppendLine().Append(node.Token.ToString()).Append(" ");
            Visit(node.Expression2, in script);
        }
        protected virtual void Visit(in AdditionOperator node, in StringBuilder script)
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
        protected virtual void Visit(in MultiplyOperator node, in StringBuilder script)
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
        protected virtual void Visit(in ComparisonOperator node, in StringBuilder script)
        {
            Visit(node.Expression1, in script);
            script.Append(" ").Append(ScriptHelper.GetComparisonLiteral(node.Token)).Append(" ");
            Visit(node.Expression2, in script);
        }
        protected virtual void Visit(in CaseExpression node, in StringBuilder script)
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
        protected virtual void Visit(in ScalarExpression node, in StringBuilder script)
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
        protected virtual void Visit(in VariableReference node, in StringBuilder script)
        {
            string name = node.Identifier;
            
            if (node.Identifier.StartsWith('&'))
            {
                name = name.Replace('&', '@');
            }
            
            script.Append(name);
        }
        protected virtual void Visit(in FunctionExpression node, in StringBuilder script)
        {
            script.Append(node.Name).Append("(");

            SyntaxNode expression;

            for (int i = 0; i < node.Parameters.Count; i++)
            {
                expression = node.Parameters[i];
                if (i > 0) { script.Append(", "); }
                Visit(in expression, in script);
            }

            script.Append(")");

            if (node.Over is not null)
            {
                script.Append(" ");
                Visit(node.Over, in script);
            }
        }
        protected virtual void Visit(in OverClause node, in StringBuilder script)
        {
            script.Append("OVER").Append("(");

            if (node.Partition is not null)
            {
                Visit(node.Partition, in script);
            }
            if (node.Order is not null)
            {
                Visit(node.Order, in script);
            }
            if (node.Preceding is not null || node.Following is not null)
            {
                script.Append(node.FrameType.ToString()).Append(" ");

                if (node.Preceding is not null && node.Following is not null)
                {
                    script.Append("BETWEEN").Append(" ");

                    Visit(node.Preceding, in script);

                    script.Append(" AND ");

                    Visit(node.Following, in script);
                }
                else if (node.Preceding is not null)
                {
                    Visit(node.Preceding, in script);
                }
            }
            script.Append(")");
        }
        protected virtual void Visit(in WindowFrame node, in StringBuilder script)
        {
            if (node.Extent == -1)
            {
                script.Append("UNBOUNDED ").Append(node.Token.ToString());
            }
            else if (node.Extent == 0)
            {
                script.Append("CURRENT ROW");
            }
            else if (node.Extent > 0)
            {
                script
                    .Append(node.Extent.ToString())
                    .Append(" ")
                    .Append(node.Token.ToString());
            }
        }
        protected virtual void Visit(in PartitionClause node, in StringBuilder script)
        {
            script.AppendLine().AppendLine("PARTITION BY");
            SyntaxNode expression;
            for (int i = 0; i < node.Columns.Count; i++)
            {
                expression = node.Columns[i];
                if (i > 0) { script.Append(", "); }
                Visit(in expression, in script);
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