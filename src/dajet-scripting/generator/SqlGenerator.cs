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
                        
                        //TODO: implement outside of this class !!!
                        ConfigureDataMapper(in select, result.Mapper);
                    }
                    else if (node is InsertStatement insert)
                    {
                        Visit(in insert, in script);
                    }
                    else if (node is UpdateStatement update)
                    {
                        Visit(in update, in script);
                    }
                    else if (node is DeleteStatement delete)
                    {
                        Visit(in delete, in script);

                        //if (delete.Output is not null)
                        //{
                        //    //TODO: implement outside of this class !!!
                        //    ConfigureOutputDataMapper(delete.Output, result.Mapper);
                        //}
                    }
                    else if (node is UpsertStatement upsert)
                    {
                        Visit(in upsert, in script);
                    }
                    else
                    {
                        Visit(in node, in script);
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
            if (statement.Select is not SelectExpression select)
            {
                if (statement.Select is not TableUnionOperator union)
                {
                    throw new InvalidOperationException("UNION operator is not found.");
                }

                if (union.Expression1 is SelectExpression)
                {
                    select = union.Expression1 as SelectExpression;
                }
                else
                {
                    select = union.Expression2 as SelectExpression;
                }
            }

            if (select is null)
            {
                throw new InvalidOperationException("SELECT statement is not defined.");
            }

            foreach (ColumnExpression column in select.Select)
            {
                DataMapper.Map(in column, in mapper);
            }
        }
        private void ConfigureOutputDataMapper(in OutputClause output, in EntityMap mapper)
        {
            foreach (ColumnExpression column in output.Columns)
            {
                DataMapper.Map(in column, in mapper);
            }
        }
        protected void Visit(in SyntaxNode expression, in StringBuilder script)
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
            else if (expression is TableVariableExpression table_variable)
            {
                Visit(in table_variable, in script);
            }
            else if (expression is TemporaryTableExpression temporary_table)
            {
                Visit(in temporary_table, in script);
            }
            else if (expression is StarExpression star)
            {
                Visit(in star, in script);
            }
            else if (expression is ValuesExpression values)
            {
                Visit(in values, in script);
            }
            else if (expression is SetExpression set)
            {
                Visit(in set, in script);
            }
        }

        #region "SELECT STATEMENT"
        protected virtual void Visit(in SelectStatement node, in StringBuilder script)
        {
            script.AppendLine();

            if (node.CommonTables is not null)
            {
                script.Append("WITH ");
                Visit(node.CommonTables, in script);
            }

            script.AppendLine();

            Visit(node.Select, in script);

            script.Append(';');
        }
        protected virtual void Visit(in SelectExpression node, in StringBuilder script)
        {
            script.Append("SELECT");

            if (node.Distinct)
            {
                script.Append(" DISTINCT");
            }

            if (node.Top is not null)
            {
                Visit(node.Top, in script);
            }
            script.AppendLine();

            for (int i = 0; i < node.Select.Count; i++)
            {
                if (i > 0) { script.Append(',').Append(Environment.NewLine); }

                Visit(node.Select[i], in script);
            }

            if (node.From is not null) { Visit(node.From, in script); }
            if (node.Where is not null) { Visit(node.Where, in script); }
            if (node.Group is not null) { Visit(node.Group, in script); }
            if (node.Having is not null) { Visit(node.Having, in script); }
            if (node.Order is not null) { Visit(node.Order, in script); }
        }
        protected virtual void Visit(in TableReference node, in StringBuilder script)
        {
            if (node.Binding is ApplicationObject entity)
            {
                script.Append(entity.TableName);
            }
            else if (node.Binding is TableExpression
                || node.Binding is CommonTableExpression
                || node.Binding is TableVariableExpression
                || node.Binding is TemporaryTableExpression)
            {
                script.Append(node.Identifier);
            }

            if (!string.IsNullOrEmpty(node.Alias))
            {
                script.Append(" AS ").Append(node.Alias);
            }
        }
        protected virtual void Visit(in ColumnExpression node, in StringBuilder script)
        {
            if (node.Expression is ColumnReference column)
            {
                Visit(in column, in script); // terminates tree traversing at column reference

                if (column.Token == TokenType.Enumeration)
                {
                    if (!string.IsNullOrEmpty(node.Alias))
                    {
                        script.Append(" AS ").Append(node.Alias);
                    }
                }
            }
            else
            {
                Visit(node.Expression, in script);

                if (!string.IsNullOrEmpty(node.Alias))
                {
                    script.Append(" AS ").Append(node.Alias);
                }
            }
        }
        protected virtual void Visit(in StarExpression node, in StringBuilder script)
        {
            script.Append(" * ");
        }
        protected virtual void Visit(in ColumnReference node, in StringBuilder script)
        {
            if (node.Mapping is not null) // we are here from anywhere, but not ColumnExpression itself
            {
                Visit(node.Mapping, in script); // terminates tree traversing at column reference
            }
            else if (node.Binding is EnumValue value)
            {
                Visit(in value, in script);
            }
        }
        protected virtual void Visit(in List<ColumnMap> mapping, in StringBuilder script)
        {
            ColumnMap column;

            for (int i = 0; i < mapping.Count; i++)
            {
                column = mapping[i];

                if (i > 0) { script.Append(", "); }

                script.Append(column.Name);

                if (!string.IsNullOrEmpty(column.Alias))
                {
                    script.Append(" AS ").Append(column.Alias);
                }
            }
        }
        protected virtual void Visit(in MetadataColumn column, in StringBuilder script, in string tableAlias)
        {
            if (!string.IsNullOrEmpty(tableAlias))
            {
                script.Append(tableAlias).Append('.');
            }
            script.Append(column.Name);
        }
        protected virtual void Visit(in MetadataProperty property, in StringBuilder script, in string tableAlias)
        {
            List<MetadataColumn> columns = property.Columns
                .OrderBy((column) => { return column.Purpose; })
                .ToList();

            MetadataColumn column;

            for (int i = 0; i < columns.Count; i++)
            {
                column = columns[i];

                if (i > 0)
                {
                    script.Append(", ");
                }

                Visit(in column, in script, in tableAlias);
            }
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

            if (node.Order is OrderClause order)
            {
                Visit(in order, in script);
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
            foreach (WhenClause when in node.CASE)
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
            else if (node.Token == TokenType.DateTime)
            {
                if (DateTime.TryParse(node.Literal, out DateTime datetime))
                {
                    script.Append($"CAST(\'{datetime.AddYears(YearOffset):yyyy-MM-ddTHH:mm:ss}\' AS datetime2)");
                }
                else
                {
                    script.Append(node.Literal);
                }
            }
            else if (node.Token == TokenType.String)
            {
                script.Append($"\'{node.Literal}\'");
            }
            else if (node.Token == TokenType.Uuid)
            {
                script.Append($"0x{ScriptHelper.GetUuidHexLiteral(new Guid(node.Literal))}");
            }
            else // Number | Binary
            {
                script.Append(node.Literal);
            }
        }
        protected virtual void Visit(in VariableReference node, in StringBuilder script)
        {
            script.Append(node.Identifier);
        }
        protected virtual void Visit(in EnumValue node, in StringBuilder script)
        {
            script.Append($"0x{ScriptHelper.GetUuidHexLiteral(node.Uuid)}");
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

            if (node.Partition is not null &&
                node.Partition.Columns is not null &&
                node.Partition.Columns.Count > 0)
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

        protected virtual void Visit(in TableVariableExpression node, in StringBuilder script)
        {
            Visit(node.Expression, in script);
        }
        protected virtual void Visit(in TemporaryTableExpression node, in StringBuilder script)
        {
            Visit(node.Expression, in script);
        }
        #endregion

        protected virtual void VisitTargetTable(in TableReference node, in StringBuilder script)
        {
            if (node.Binding is ApplicationObject entity)
            {
                script.Append(entity.TableName);
            }
            else if (node.Binding is TableExpression
                || node.Binding is CommonTableExpression
                || node.Binding is TableVariableExpression
                || node.Binding is TemporaryTableExpression)
            {
                script.Append(node.Identifier);
            }
            else
            {
                throw new InvalidOperationException("DML: Target table identifier is missing.");
            }
        }

        #region "INSERT STATEMENT"
        protected virtual void Visit(in InsertStatement node, in StringBuilder script)
        {
            if (node.Target.Binding is CommonTableExpression)
            {
                throw new InvalidOperationException("INSERT: computed table (cte) targeting is not allowed.");
            }

            script.AppendLine();

            if (node.CommonTables is not null)
            {
                script.Append("WITH ");
                Visit(node.CommonTables, in script);
            }

            script.AppendLine().Append("INSERT INTO ");

            VisitTargetTable(node.Target, in script);
            
            script.Append(' ');

            if (node.Columns.Count > 0)
            {
                script.Append('(');

                ColumnReference column;

                for (int i = 0; i < node.Columns.Count; i++)
                {
                    column = node.Columns[i];
                    
                    if (i > 0) { script.Append(", "); }
                    
                    Visit(in column, in script);
                }

                script.Append(')');
            }

            script.AppendLine();

            Visit(node.Source, in script);

            script.Append(';').AppendLine();
        }
        protected virtual void Visit(in ValuesExpression node, in StringBuilder script)
        {
            script.Append("VALUES(");

            SyntaxNode value;

            for (int i = 0; i < node.Values.Count; i++)
            {
                value = node.Values[i];

                if (i > 0) { script.Append(", "); }

                Visit(in value, in script);
            }

            script.Append(")").AppendLine();
        }
        #endregion

        #region "UPDATE STATEMENT"
        protected virtual void Visit(in UpdateStatement node, in StringBuilder script)
        {
            if (node.Target.Binding is CommonTableExpression)
            {
                throw new InvalidOperationException("UPDATE: computed table (cte) targeting is not allowed.");
            }

            script.AppendLine();

            if (node.CommonTables is not null)
            {
                script.Append("WITH ");
                Visit(node.CommonTables, in script);
            }

            script.AppendLine().Append("UPDATE ");

            VisitTargetTable(node.Target, in script);

            script.AppendLine().Append("SET ");

            SetExpression set;
            
            for (int i = 0; i < node.Set.Count; i++)
            {
                set = node.Set[i];

                if (i > 0) { script.Append(","); }

                Visit(in set, in script);
            }

            script.AppendLine();

            if (node.Source is not null)
            {
                Visit(node.Source, in script);
            }

            if (node.Where is not null)
            {
                Visit(node.Where, in script);
            }

            script.Append(';').AppendLine();
        }
        protected virtual void Visit(in SetExpression node, in StringBuilder script)
        {
            script.AppendLine();
            Visit(node.Column, in script);
            script.Append(" = ");
            Visit(node.Initializer, in script);
        }
        #endregion

        #region "DELETE STATEMENT"
        protected virtual void Visit(in DeleteStatement node, in StringBuilder script)
        {
            if (node.Target.Binding is CommonTableExpression)
            {
                throw new InvalidOperationException("DELETE: computed table (cte) targeting is not allowed.");
            }

            script.AppendLine();

            if (node.CommonTables is not null)
            {
                script.Append("WITH ");

                Visit(node.CommonTables, in script);
            }

            script.Append("DELETE FROM ");

            VisitTargetTable(node.Target, in script);

            if (node.Where is not null)
            {
                Visit(node.Where, script);
            }

            script.Append(';').AppendLine();
        }
        protected virtual void Visit(in OutputClause node, in StringBuilder script)
        {
            script.AppendLine().AppendLine("OUTPUT");

            for (int i = 0; i < node.Columns.Count; i++)
            {
                if (i > 0) { script.Append(", "); }

                Visit(node.Columns[i], in script);
            }
        }
        #endregion

        #region "UPSERT STATEMENT"
        protected virtual void Visit(in UpsertStatement node, in StringBuilder script)
        {
            script.AppendLine();

            if (node.CommonTables is not null)
            {
                script.Append("WITH ");
                Visit(node.CommonTables, in script);
            }

            script.AppendLine().Append("UPSERT ");

            Visit(node.Target, in script);

            if (node.IgnoreUpdate) { script.Append(" IGNORE UPDATE"); }

            if (node.Where is not null) { Visit(node.Where, in script); }
            else { throw new InvalidOperationException("UPSERT: WHERE clause is not defined."); }

            if (node.Set is not null && node.Set.Count > 0)
            {
                script.AppendLine().Append("SET ");
                SetExpression set;
                for (int i = 0; i < node.Set.Count; i++)
                {
                    set = node.Set[i];
                    if (i > 0) { script.Append(","); }
                    Visit(in set, in script);
                }
                script.AppendLine();
            }
            else
            {
                throw new InvalidOperationException("UPSERT: SET clause is not defined.");
            }

            if (node.Source is not null) { Visit(node.Source, in script); }
            else { throw new InvalidOperationException("UPSERT: FROM clause is not defined."); }

            script.Append(';').AppendLine();
        }
        #endregion
    }
}