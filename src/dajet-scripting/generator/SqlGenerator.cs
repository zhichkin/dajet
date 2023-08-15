using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using System.Data;
using System.Text;

namespace DaJet.Scripting
{
    public abstract class SqlGenerator : ISqlGenerator
    {
        protected IMetadataProvider Metadata { get; private set; }
        public int YearOffset { get; set; } = 0;
        public bool TryGenerate(in ScriptModel model, in IMetadataProvider metadata, out GeneratorResult result)
        {
            Metadata = metadata;

            result = new GeneratorResult();

            result.Mapper.YearOffset = YearOffset;

            try
            {
                StringBuilder script = new();

                foreach (SyntaxNode node in model.Statements)
                {
                    if (!(node is DeclareStatement || node is CommentStatement))
                    {
                        script.AppendLine();
                    }

                    if (node is SelectStatement select)
                    {
                        Visit(in select, in script);
                        
                        //TODO: implement outside of this class !!!
                        ConfigureDataMapper(in select, result.Mapper);

                        EntityMap mapper = new();
                        ConfigureDataMapper(in select, mapper);
                        if (mapper.Properties.Count > 0)
                        {
                            result.Commands.Add(new ScriptCommand()
                            {
                                Mapper = mapper
                            });
                        }
                    }
                    else if (node is ConsumeStatement consume)
                    {
                        Visit(in consume, in script);

                        EntityMap mapper = new();
                        ConfigureDataMapper(in consume, mapper);
                        result.Mapper = mapper;

                        if (mapper.Properties.Count > 0)
                        {
                            ScriptCommand command = new()
                            {
                                Mapper = mapper
                            };
                            result.Commands.Add(command);
                        }
                    }
                    else if (node is DeleteStatement delete)
                    {
                        Visit(in delete, in script);

                        if (delete.Output is not null)
                        {
                            //TODO: implement outside of this class !!!
                            ConfigureDataMapper(delete.Output, result.Mapper);

                            EntityMap mapper = new();
                            ConfigureDataMapper(delete.Output, mapper);
                            if (mapper.Properties.Count > 0)
                            {
                                result.Commands.Add(new ScriptCommand()
                                {
                                    Mapper = mapper
                                });
                            }
                        }
                    }
                    else if (node is InsertStatement insert) { Visit(in insert, in script); }
                    else if (node is UpdateStatement update) { Visit(in update, in script); }
                    else if (node is UpsertStatement upsert) { Visit(in upsert, in script); }
                    else
                    {
                        Visit(in node, in script); // non query commands !?
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
        public bool TryGenerate(in ScriptModel model, in IMetadataProvider metadata, out List<ScriptCommand> commands, out string error)
        {
            Metadata = metadata;
            error = string.Empty;
            commands = new List<ScriptCommand>();

            try
            {
                foreach (SyntaxNode node in model.Statements)
                {
                    if (node is SelectStatement select)
                    {
                        commands.Add(GenerateScriptCommand(in select));
                    }
                    else if (node is ConsumeStatement consume)
                    {
                        commands.Add(GenerateScriptCommand(in consume));
                    }
                    else if (node is DeleteStatement delete)
                    {
                        commands.Add(GenerateScriptCommand(in delete));
                    }
                    else
                    {
                        commands.Add(GenerateScriptCommand(in node));
                    }
                }
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return string.IsNullOrEmpty(error);
        }
        private ScriptCommand GenerateScriptCommand(in SyntaxNode node)
        {
            StringBuilder script = new();
            
            Visit(in node, in script);

            EntityMap mapper = new()
            {
                YearOffset = Metadata.YearOffset
            };

            return new ScriptCommand()
            {
                Name = string.Empty,
                Mapper = mapper,
                Script = script.ToString(),
                Statement = node
            };
        }
        private ScriptCommand GenerateScriptCommand(in SelectStatement select)
        {
            StringBuilder script = new();

            Visit(in select, in script);

            EntityMap mapper = new()
            {
                YearOffset = Metadata.YearOffset
            };

            ConfigureDataMapper(in select, in mapper);

            if (!TryGetFromTable(select, out TableReference table))
            {
                throw new InvalidOperationException();
            }

            ScriptHelper.GetColumnIdentifiers(table.Identifier, out _, out string tableName);

            return new ScriptCommand()
            {
                Mapper = mapper,
                Script = script.ToString(),
                Statement = select,
                Name = (string.IsNullOrEmpty(table.Alias) ? tableName : table.Alias)
            };
        }
        private ScriptCommand GenerateScriptCommand(in ConsumeStatement consume)
        {
            StringBuilder script = new();

            Visit(in consume, in script);

            EntityMap mapper = new()
            {
                YearOffset = Metadata.YearOffset
            };

            ConfigureDataMapper(in consume, in mapper);

            if (!TryGetFromTable(consume, out TableReference table))
            {
                throw new InvalidOperationException();
            }

            ScriptHelper.GetColumnIdentifiers(table.Identifier, out _, out string tableName);

            return new ScriptCommand()
            {
                Mapper = mapper,
                Script = script.ToString(),
                Statement = consume,
                Name = (string.IsNullOrEmpty(table.Alias) ? tableName : table.Alias)
            };
        }
        private ScriptCommand GenerateScriptCommand(in DeleteStatement delete)
        {
            StringBuilder script = new();

            Visit(in delete, in script);

            EntityMap mapper = new()
            {
                YearOffset = Metadata.YearOffset
            };

            if (delete.Output is not null)
            {
                ConfigureDataMapper(delete.Output, in mapper);
            }

            TableReference table = delete.Target;

            ScriptHelper.GetColumnIdentifiers(table.Identifier, out _, out string tableName);

            return new ScriptCommand()
            {
                Mapper = mapper,
                Script = script.ToString(),
                Statement = delete,
                Name = (string.IsNullOrEmpty(table.Alias) ? tableName : table.Alias)
            };
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

            if (select.Into is not null)
            {
                return; // SELECT ... INTO ... statement does not return any records
            }

            foreach (ColumnExpression column in select.Select)
            {
                DataMapper.Map(in column, in mapper);
            }
        }
        private void ConfigureDataMapper(in ConsumeStatement statement, in EntityMap mapper)
        {
            if (statement.Into is not null)
            {
                return; // CONSUME ... INTO ... statement returns data into temporary table
            }

            foreach (ColumnExpression column in statement.Columns)
            {
                DataMapper.Map(in column, in mapper);
            }
        }
        private void ConfigureDataMapper(in OutputClause output, in EntityMap mapper)
        {
            if (output.Into is not null)
            {
                return; // OUTPUT ... INTO ... statement does not return any records
            }

            foreach (ColumnExpression column in output.Columns)
            {
                DataMapper.Map(in column, in mapper);
            }
        }
        protected void Visit(in SyntaxNode expression, in StringBuilder script)
        {
            if (expression is GroupOperator group) { Visit(in group, in script); }
            else if (expression is UnaryOperator unary) { Visit(in unary, in script); }
            else if (expression is BinaryOperator binary) { Visit(in binary, in script); }
            else if (expression is AdditionOperator addition) { Visit(in addition, in script); }
            else if (expression is MultiplyOperator multiply) { Visit(in multiply, in script); }
            else if (expression is ComparisonOperator comparison) { Visit(in comparison, in script); }
            else if (expression is CaseExpression case_when) { Visit(in case_when, in script); }
            else if (expression is ScalarExpression scalar) { Visit(in scalar, in script); }
            else if (expression is VariableReference variable) { Visit(in variable, in script); }
            else if (expression is SelectExpression select) { Visit(in select, in script); }
            else if (expression is TableJoinOperator join) { Visit(in join, in script); }
            else if (expression is TableUnionOperator union) { Visit(in union, in script); }
            else if (expression is TableExpression derived) { Visit(in derived, in script); }
            else if (expression is TableReference table) { Visit(in table, in script); }
            else if (expression is ColumnReference column) { Visit(in column, in script); }
            else if (expression is FunctionExpression function) { Visit(in function, in script); }
            else if (expression is TableVariableExpression table_variable) { Visit(in table_variable, in script); }
            else if (expression is TemporaryTableExpression temporary_table) { Visit(in temporary_table, in script); }
            else if (expression is StarExpression star) { Visit(in star, in script); }
            else if (expression is SetExpression set) { Visit(in set, in script); }
            else if (expression is InsertStatement insert) { Visit(in insert, in script); }
            else if (expression is UpdateStatement update) { Visit(in update, in script); }
            else if (expression is DeleteStatement delete) { Visit(in delete, in script); }
            else if (expression is CreateTypeStatement type) { Visit(in type, in script); }
            else if (expression is CreateSequenceStatement sequence) { Visit(in sequence, in script); }
        }

        public abstract void Visit(in CreateTypeStatement node, in StringBuilder script);
        public abstract void Visit(in CreateSequenceStatement node, in StringBuilder script);

        #region "SELECT STATEMENT"
        protected virtual void Visit(in SelectStatement node, in StringBuilder script)
        {
            if (node.CommonTables is not null)
            {
                script.Append("WITH ");
                Visit(node.CommonTables, in script);
            }

            script.AppendLine();

            Visit(node.Select, in script);
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
                if (i > 0) { script.AppendLine(","); }

                Visit(node.Select[i], in script);
            }

            if (node.Into is not null) { Visit(node.Into, in script); }
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
            script.Append("*");
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
        protected virtual void Visit(in IntoClause node, in StringBuilder script)
        {
            script.AppendLine().Append("INTO ");
            Visit(node.Table, in script);
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
            if (node is null || node.Expressions is null || node.Expressions.Count == 0)
            {
                return;
            }

            script.AppendLine().AppendLine("ORDER BY");

            OrderExpression order;

            string separator = ", ";

            for (int i = 0; i < node.Expressions.Count; i++)
            {
                order = node.Expressions[i];

                if (i > 0) { script.Append(separator); }

                if (order.Expression is ColumnReference column && column.Mapping is not null && column.Mapping.Count > 1)
                {
                    ColumnMap field;

                    for (int f = 0; f < column.Mapping.Count; f++)
                    {
                        field = column.Mapping[f];

                        if (f > 0) { script.Append(", "); }

                        script.Append(field.Name);

                        if (order.Token == TokenType.DESC)
                        {
                            script.Append(" DESC");
                        }
                        else
                        {
                            script.Append(" ASC"); // default
                        }
                    }
                }
                else
                {
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
            }

            if (node.Offset is not null)
            {
                script.AppendLine();

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
            string name = node.Name.ToUpperInvariant();

            if (name == "UUIDOF")
            {
                VisitUuidOf(in node, in script);
            }
            else if (name == "TYPEOF")
            {
                VisitTypeOf(in node, in script);
            }
            else
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
                script.Append(' ').Append(node.FrameType.ToString()).Append(' ');

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

        #region "CONSUME STATEMENT"
        protected virtual void Visit(in ConsumeStatement node, in StringBuilder script)
        {
            script.Append("SELECT");

            if (node.Top is not null) { Visit(node.Top, in script); }

            script.AppendLine();

            for (int i = 0; i < node.Columns.Count; i++)
            {
                if (i > 0) { script.Append(',').Append(Environment.NewLine); }

                Visit(node.Columns[i], in script);
            }

            if (node.Into is not null) { Visit(node.Into, in script); }
            if (node.From is not null) { Visit(node.From, in script); }
            if (node.Where is not null) { Visit(node.Where, in script); }
            if (node.Order is not null) { Visit(node.Order, in script); }
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
        protected void ConfigureTableAlias(in SyntaxNode node)
        {
            if (node is TableReference table && string.IsNullOrEmpty(table.Alias))
            {
                if (table.Binding is TableVariableExpression || table.Binding is TemporaryTableExpression)
                {
                    table.Alias = table.Identifier;
                }
            }
            else if (node is TableExpression expression && string.IsNullOrEmpty(expression.Alias))
            {
                throw new InvalidOperationException("Derived table alias is not defined.");
            }
        }
        protected string[] GetInsertSelectColumnLists(in SyntaxNode target, in SyntaxNode source)
        {
            List<PropertyMappingRule> rules = DataMapper.CreateMappingRules(in target, in source, null);

            StringBuilder insert = new();
            StringBuilder select = new();

            foreach (PropertyMappingRule rule in rules)
            {
                if (rule.Target.IsDbGenerated) { continue; } // _Version binary(8)

                foreach (ColumnMappingRule map in rule.Columns)
                {
                    // INSERT column list
                    if (insert.Length > 0) { insert.Append(", "); }

                    insert.Append(map.Target.Name);

                    // SELECT column list
                    if (select.Length > 0) { select.Append(", "); }

                    if (map.Source is ColumnMap source_column)
                    {
                        select.Append(source_column.Alias);
                    }
                    else if (map.Source is ScalarExpression scalar)
                    {
                        Visit(in scalar, select);
                    }
                }
            }

            return new string[] { insert.ToString(), select.ToString() };
        }
        protected void TransformSetClause(in SyntaxNode target, in SyntaxNode source, in List<SetExpression> set_clause, in StringBuilder script)
        {
            List<PropertyMappingRule> rules = DataMapper.CreateMappingRules(in target, in source, in set_clause);

            string target_table = string.Empty;
            string source_table = string.Empty;

            if (target is TableReference table)
            {
                if (string.IsNullOrEmpty(table.Alias))
                {
                    target_table = table.Identifier;
                }
                else
                {
                    target_table = table.Alias;
                }
            }

            if (source is TableReference table2)
            {
                if (string.IsNullOrEmpty(table2.Alias))
                {
                    source_table = table2.Identifier;
                }
                else
                {
                    source_table = table2.Alias;
                }
            }
            else if (source is TableExpression expression)
            {
                source_table = expression.Alias;
            }

            foreach (PropertyMappingRule rule in rules)
            {
                for (int s = 0; s < set_clause.Count; s++)
                {
                    SetExpression set = set_clause[s];

                    if (rule.Target.Name == set.Column.GetName())
                    {
                        if (s > 0) { script.AppendLine(","); }

                        for (int i = 0; i < rule.Columns.Count; i++)
                        {
                            ColumnMappingRule map = rule.Columns[i];

                            if (i > 0) { script.AppendLine(","); }

                            if (!string.IsNullOrEmpty(target_table))
                            {
                                script.Append(target_table).Append('.');
                            }
                            script.Append(map.Target.Name);

                            script.Append(" = ");

                            if (map.Source is ColumnMap column)
                            {
                                if (!string.IsNullOrEmpty(source_table))
                                {
                                    script.Append(source_table).Append('.');
                                }
                                script.Append(column.Alias);
                            }
                            else if (map.Source is ScalarExpression scalar)
                            {
                                Visit(in scalar, script);
                            }
                        }
                    }
                }
            }
        }

        #region "INSERT STATEMENT"
        protected virtual void Visit(in InsertStatement node, in StringBuilder script)
        {
            if (node.Target.Binding is CommonTableExpression)
            {
                throw new InvalidOperationException("INSERT: computed table (cte) targeting is not allowed.");
            }

            ConfigureTableAlias(node.Source); // @variable and #temporary tables

            InsertStatementTransformer transformer = new();
            
            transformer.Transform(node);

            if (node.CommonTables is not null)
            {
                script.Append("WITH ");
                Visit(node.CommonTables, in script);
            }

            string[] columns = GetInsertSelectColumnLists(node.Target, node.Source);

            string vectorColumn = transformer.GetVectorColumnName(in node);

            if (vectorColumn is not null)
            {
                columns[1] = columns[1].Replace(vectorColumn, VisitVectorFunction(node.Target));
            }

            script.AppendLine().Append("INSERT INTO ");
            VisitTargetTable(node.Target, in script);
            script.Append(' ');
            script.Append('(');
            script.Append(columns[0]);
            script.AppendLine(")");

            if (node.Source is TableReference table) // CTE, @variable or #temporary tables
            {
                script.AppendLine("SELECT");
                script.AppendLine(columns[1]);
                script.Append("FROM ");
                Visit(in table, in script);
            }
            else if (node.Source is TableExpression select) // Derived table: SELECT...FROM (SELECT...) AS source
            {
                script.AppendLine("SELECT ");
                script.AppendLine(columns[1]);
                script.Append("FROM ");
                Visit(in select, in script);
            }
            else // SELECT expression - convert to derived table to ensure proper column order
            {
                script.AppendLine("SELECT ");
                script.AppendLine(columns[1]);
                script.Append("FROM (");
                Visit(node.Source, in script);
                script.Append(") AS source");
            }

            script.Append(";");
        }
        private string VisitVectorFunction(in SyntaxNode target)
        {
            FunctionExpression function = new()
            {
                Name = "VECTOR"
            };

            if (target is TableReference table && table.Binding is ApplicationObject entity)
            {
                function.Parameters.Add(new ScalarExpression()
                {
                    Token = TokenType.String,
                    Literal = $"{entity.TableName}_so"
                });
            }
            
            StringBuilder script = new();
            Visit(in function, in script);
            return script.ToString();
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

            script.Append(")");
        }
        #endregion

        #region "UPDATE STATEMENT"
        protected virtual void Visit(in UpdateStatement node, in StringBuilder script)
        {
            if (node.Target.Binding is CommonTableExpression)
            {
                throw new InvalidOperationException("UPDATE: computed table (cte) targeting is not allowed.");
            }

            ConfigureTableAlias(node.Source); // @variable and #temporary tables

            if (node.CommonTables is not null)
            {
                script.Append("WITH ");
                Visit(node.CommonTables, in script);
            }

            script.AppendLine().Append("UPDATE ");
            VisitTargetTable(node.Target, in script);

            if (node.Hints is not null && node.Hints.Count > 0)
            {
                // MS SQL Server: UPDLOCK, SERIALIZABLE and so on ...
                script.Append(" WITH (");
                for (int i = 0; i < node.Hints.Count; i++)
                {
                    if (i > 0) { script.Append(", "); }
                    script.Append(node.Hints[i]);
                }
                script.Append(')');
            }

            script.AppendLine().Append("SET ");
            Visit(node.Set, in script);

            if (node.Source is not null)
            {
                script.AppendLine();
                script.Append($"FROM ");
                Visit(node.Source, in script);
            }

            if (node.Where is not null)
            {
                Visit(node.Where, in script);
            }
        }
        protected virtual void Visit(in SetClause node, in StringBuilder script)
        {
            // NOTE: SET expression initializer could be as follows (currently only ColumnReference is implemented):
            // ColumnReference, ScalarExpression, VariableReference, FunctionExpression, CaseExpression, EnumValue

            SetExpression set;
            for (int i = 0; i < node.Expressions.Count; i++)
            {
                set = node.Expressions[i];
                if (i > 0) { script.Append(","); }
                Visit(in set, in script);
            }
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

            if (node.CommonTables is not null)
            {
                script.Append("WITH ");

                Visit(node.CommonTables, in script);
            }

            script.Append("DELETE FROM ");

            VisitTargetTable(node.Target, in script);

            if (node.Output is not null)
            {
                Visit(node.Output, script);
            }

            if (node.Where is not null)
            {
                Visit(node.Where, script);
            }
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
            if (node.Target.Binding is CommonTableExpression)
            {
                throw new InvalidOperationException("UPSERT: computed table (cte) targeting is not allowed.");
            }

            //TODO: UPSERT - optional SET clause if IGNORE UPDATE is used
            if (node.Set is null || node.Set.Expressions.Count == 0)
            {
                throw new InvalidOperationException("UPSERT: SET clause is not defined.");
            }

            if (node.Source is null)
            {
                throw new InvalidOperationException("UPSERT: FROM clause is not defined.");
            }

            // INSERT STATEMENT

            StringBuilder insert_script = new();

            insert_script.AppendLine();

            InsertStatement insert = new()
            {
                CommonTables = node.CommonTables,
                Target = node.Target,
                Source = node.Source
            };

            Visit(in insert, in insert_script);

            insert_script.AppendLine().Append($"WHERE NOT EXISTS (SELECT 1 FROM ");
            Visit(node.Target, in insert_script);
            insert_script.Append(' ');
            Visit(node.Where, in insert_script);
            insert_script.Append(')');

            // UPDATE STATEMENT

            if (!node.IgnoreUpdate)
            {
                UpdateStatement update = new()
                {
                    CommonTables = node.CommonTables,
                    Target = node.Target,
                    Source = node.Source,
                    Where = node.Where,
                    Set = node.Set,
                    Hints = node.Hints
                };

                // change all ColumnMap identifiers in ColumnReference nodes, which are referencing ColumnExpression of the Source
                // to avoid ambiguous column names when they are the same for both Target and Source (WHERE clause)
                new UpdateStatementTransformer().Transform(update);

                Visit(in update, in script); script.Append(';');
            }

            script.Append(insert_script);
        }
        #endregion

        protected void VisitUuidOf(in FunctionExpression node, in StringBuilder script)
        {
            if (node.Parameters.Count == 0)
            {
                throw new FormatException($"Function {node.Name}: missing parameter.");
            }

            if (node.Parameters[0] is not ColumnReference column)
            {
                throw new FormatException($"Function {node.Name}: invalid parameter (must be column reference).");
            }

            if (column.Mapping is not null)
            {
                if (column.Mapping.Count == 1)
                {
                    if (column.Mapping[0].Type != UnionTag.Entity)
                    {
                        throw new FormatException($"Function {node.Name}: invalid parameter data type.");
                    }
                }
                else
                {
                    ColumnMap map = null;

                    for (int i = 0; i < column.Mapping.Count; i++)
                    {
                        if (column.Mapping[i].Type == UnionTag.Entity)
                        {
                            map = column.Mapping[i]; break;
                        }
                    }

                    if (map is null)
                    {
                        throw new FormatException($"Function {node.Name}: invalid parameter data type.");
                    }

                    column.Mapping.Clear();
                    column.Mapping.Add(map);
                }
            }
            
            Visit(in column, in script);
        }
        protected void VisitTypeOf(in FunctionExpression node, in StringBuilder script)
        {
            if (node.Parameters.Count == 0)
            {
                throw new FormatException($"Function {node.Name}: missing parameter.");
            }
            
            if (node.Parameters[0] is not ColumnReference column)
            {
                throw new FormatException($"Function {node.Name}: invalid parameter.");
            }

            if (column.Mapping is not null)
            {
                if (column.Mapping.Count == 1)
                {
                    if (column.Mapping[0].Type != UnionTag.Entity)
                    {
                        throw new FormatException($"Function {node.Name}: invalid parameter data type.");
                    }
                    
                    if (column.Binding is not MetadataProperty property)
                    {
                        throw new FormatException($"Function {node.Name}: invalid parameter binding.");
                    }

                    ScalarExpression scalar = new()
                    {
                        Token = TokenType.Number,
                        Literal = property.PropertyType.TypeCode.ToString()
                    };
                    Visit(in scalar, in script);
                    return;
                }
                else
                {
                    ColumnMap map = null;

                    for (int i = 0; i < column.Mapping.Count; i++)
                    {
                        if (column.Mapping[i].Type == UnionTag.TypeCode)
                        {
                            map = column.Mapping[i]; break;
                        }
                    }

                    if (map is null)
                    {
                        throw new FormatException($"Function {node.Name}: invalid parameter data type.");
                    }

                    column.Mapping.Clear();
                    column.Mapping.Add(map);
                }
            }

            Visit(in column, in script);
        }

        protected bool TryGetFromTable(in SyntaxNode node, out TableReference table)
        {
            table = null;

            if (node is SelectStatement statement)
            {
                return TryGetFromTable(statement.Select, out table);
            }
            else if (node is SelectExpression select)
            {
                return TryGetFromTable(select.From, out table);
            }
            else if (node is ConsumeStatement consume)
            {
                return TryGetFromTable(consume.From, out table);
            }

            return (table is not null);
        }
        protected bool TryGetFromTable(in FromClause from, out TableReference table)
        {
            return TryGetFromTableRecursively(from.Expression, out table);
        }
        protected bool TryGetFromTableRecursively(in SyntaxNode node, out TableReference table)
        {
            table = null;

            if (node is TableJoinOperator join)
            {
                return TryGetFromTableRecursively(join.Expression1, out table);
            }
            else if (node is TableReference target)
            {
                table = target;
            }

            return (table is not null);
        }

        #region "CREATE TYPE DEFINITION FROM ColumnExpression"
        protected CreateTypeStatement CreateTypeDefinition(in string identifier, in List<ColumnExpression> properties)
        {
            CreateTypeStatement type = new()
            {
                Identifier = identifier
            };

            foreach (ColumnExpression property in properties)
            {
                List<ColumnDefinition> columns = CreateColumnDefinitions(in property);

                foreach (ColumnDefinition column in columns)
                {
                    type.Columns.Add(column);
                }
            }

            return type;
        }
        protected List<ColumnDefinition> CreateColumnDefinitions(in ColumnExpression property)
        {
            List<ColumnDefinition> columns = new();

            string columnName = property.Alias;

            Infer(property.Expression, in columns, ref columnName);

            foreach (ColumnDefinition column in columns)
            {
                string postfix = (string.IsNullOrEmpty(column.Name) ? string.Empty : "_" + column.Name);

                if (string.IsNullOrEmpty(property.Alias))
                {
                    column.Name = columnName + postfix;
                }
                else
                {
                    column.Name = property.Alias + postfix;
                }
            }

            return columns;
        }
        private void Infer(in SyntaxNode node, in List<ColumnDefinition> columns, ref string name)
        {
            if (node is ColumnExpression property) { Infer(in property, in columns, ref name); }
            else if (node is ColumnReference column) { Infer(in column, in columns, ref name); }
            else if (node is ScalarExpression scalar) { Infer(in scalar, in columns, ref name); }
            else if (node is VariableReference variable) { Infer(in variable, in columns, ref name); }
            else if (node is CaseExpression _case) { Infer(in _case, in columns, ref name); }
            else if (node is FunctionExpression function) { Infer(in function, in columns, ref name); }
        }
        protected virtual void Infer(in ColumnExpression column, in List<ColumnDefinition> columns, ref string name)
        {
            Infer(column.Expression, in columns, ref name);

            if (!string.IsNullOrEmpty(column.Alias))
            {
                name = column.Alias;
            }
        }
        protected virtual void Infer(in ColumnReference column, in List<ColumnDefinition> columns, ref string name)
        {
            if (column.Binding is ColumnExpression parent)
            {
                Infer(in parent, in columns, ref name);
            }
            else if (column.Binding is EnumValue)
            {
                TypeIdentifier type = new()
                {
                    Binding = typeof(Guid), // pg = bytea
                    Identifier = "uuid"     // ms = binary(16)
                    //Qualifier1 = 16;
                };
                columns.Add(new ColumnDefinition() { Type = type });
            }
            else if (column.Binding is MetadataProperty property)
            {
                Infer(in property, in columns, ref name);
            }
            else
            {
                throw new InvalidOperationException($"Failed to create column definition for identifier [{column.Identifier}]");
            }
        }
        protected virtual void Infer(in MetadataProperty property, in List<ColumnDefinition> columns, ref string name)
        {
            name = property.Name;

            List<MetadataColumn> fields = property.Columns.OrderBy((column) => { return column.Purpose; }).ToList();

            for (int i = 0; i < fields.Count; i++)
            {
                MetadataColumn field = fields[i];

                string columnName = UnionType.GetPurposeLiteral(field.Purpose);

                ColumnDefinition column = new()
                {
                    Name = columnName,
                    Type = new TypeIdentifier()
                    {
                        Identifier = field.TypeName
                    }
                };

                if (field.Length == -1)
                {
                    column.Type.Identifier += "(max)";
                }
                else if (field.Length > 0)
                {
                    column.Type.Identifier += $"({field.Length})";
                }
                else if (field.TypeName == "numeric")
                {
                    column.Type.Identifier += $"({field.Precision},{field.Scale})";
                }

                columns.Add(column);
            }
        }
        protected virtual void Infer(in ScalarExpression scalar, in List<ColumnDefinition> columns, ref string name)
        {
            //TODO: implement void Infer(in ScalarExpression scalar, in List<ColumnDefinition> columns)

            if (scalar.Token == TokenType.Boolean)
            {
                
            }
            else if (scalar.Token == TokenType.Number)
            {
                
            }
            else if (scalar.Token == TokenType.DateTime)
            {
                
            }
            else if (scalar.Token == TokenType.String)
            {
                
            }
            else if (scalar.Token == TokenType.Binary)
            {
                
            }
            else if (scalar.Token == TokenType.Uuid)
            {
                
            }
            else if (scalar.Token == TokenType.Entity)
            {
                if (Entity.TryParse(scalar.Literal, out Entity entity))
                {
                    
                }
            }
            else if (scalar.Token == TokenType.Version)
            {
                
            }
            else if (scalar.Token == TokenType.Integer)
            {
                
            }
            else if (scalar.Token == TokenType.NULL)
            {
                
            }
        }
        protected virtual void Infer(in VariableReference identifier, in List<ColumnDefinition> columns, ref string name)
        {
            //TODO: implement void Infer(in VariableReference identifier, in List<ColumnDefinition> columns)

            if (identifier.Binding is Entity entity)
            {
                return;
            }

            if (identifier.Binding is not Type type)
            {
                return;
            }

            if (type == typeof(Guid))
            {
                
            }
            else if (type == typeof(bool))
            {
                
            }
            else if (type == typeof(decimal))
            {
                
            }
            else if (type == typeof(DateTime))
            {
                
            }
            else if (type == typeof(string))
            {
                
            }
            else if (type == typeof(byte[]))
            {
                
            }
            else if (type == typeof(ulong))
            {
                
            }
            else if (type == typeof(int))
            {
                
            }
        }
        protected virtual void Infer(in CaseExpression node, in List<ColumnDefinition> columns, ref string name)
        {
            if (node.CASE is not null && node.CASE.Count > 0)
            {
                WhenClause when = node.CASE[0];
                Infer(when.THEN, in columns, ref name);
            }
            
            //NOTE: WHEN clause is not used for type inference
            //NOTE: ELSE clause is not used for type inference
        }
        protected virtual void Infer(in FunctionExpression function, in List<ColumnDefinition> columns, ref string name)
        {
            ColumnDefinition column = new()
            {
                Name = string.Empty,
                Type = new TypeIdentifier()
            };

            string functionName = function.Name.ToUpperInvariant();

            if (functionName == "COUNT")
            {
                //union.IsInteger = true; return;
            }
            else if (functionName == "ROW_NUMBER")
            {
                //TODO: IsVersion is int64 (bigint) hack
                //NOTE: the function does not have any parameters
                //union.IsVersion = true; return;
            }
            else if (functionName == "DATALENGTH")
            {
                //TODO: IsInteger is int32 (int) hack
                //NOTE: the function have one parameter, but we ignore it
                column.Type.Identifier = "int";
                columns.Add(column);
                return;
            }
            else if (functionName == "OCTET_LENGTH")
            {
                //TODO: IsInteger is int32 (int) hack
                //NOTE: the function have one parameter, but we ignore it
                column.Type.Identifier = "integer";
                columns.Add(column);
                return;
            }
            else if (functionName == "SUBSTRING")
            {
                //union.IsString = true; return;
            }
            else if (functionName == "NOW")
            {
                //union.IsDateTime = true; return;
            }
            else if (functionName == "UUIDOF")
            {
                //union.IsUuid = true; return;
            }
            else if (functionName == "TYPEOF")
            {
                //union.IsInteger = true; return;
            }
            else if (name == "VECTOR")
            {
                //union.IsNumeric = true; return;
            }

            foreach (SyntaxNode parameter in function.Parameters)
            {
                Infer(in parameter, in columns, ref name);
            }
        }
        #endregion
    }
}