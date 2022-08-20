using DaJet.Data.Mapping;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class MsSqlGenerator
    {
        public bool TryGenerate(in ScriptModel model, out GeneratorResult result)
        {
            result = new();

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

        #region "SELECT STATEMENT"

        #region "SELECT AND FROM CLAUSE"

        private void VisitSelectStatement(SelectStatement select, StringBuilder script, EntityMap mapper)
        {
            VisitSelectClause(select, script, mapper);

            VisitFromClause(select.FROM, script);

            if (select.WHERE != null) // optional
            {
                VisitWhereClause(select.WHERE, script);
            }
        }
        private void VisitSelectClause(SelectStatement select, StringBuilder script, EntityMap mapper)
        {
            script.AppendLine("SELECT");

            List<string> columns = new();

            foreach (SyntaxNode node in select.SELECT)
            {
                if (node is not Identifier identifier)
                {
                    continue;
                }

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

            script.AppendJoin("," + Environment.NewLine, columns).AppendLine();
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
            else if (node is SubqueryExpression subquery)
            {
                VisitSubqueryExpression(subquery, script);
            }
            else if (node is Identifier table && table.Token == TokenType.Table)
            {
                VisitTableIdentifier(table, script);
            }
        }

        private void VisitTableIdentifier(Identifier table, StringBuilder script)
        {
            if (table.Tag is not ApplicationObject entity)
            {
                return;
            }

            script.Append(entity.TableName);

            if (!string.IsNullOrWhiteSpace(table.Alias))
            {
                script.Append(" AS ").Append(table.Alias);
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
                VisitBooleanExpression(group.Expression, script);
                script.Append(")");
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
            if (node.Expression1 is Identifier identifier1)
            {
                VisitIdentifier(identifier1, script);
            }
            else if (node.Expression1 is ScalarExpression scalar1)
            {
                VisitScalarExpression(scalar1, script);
            }
            else
            {
                VisitBooleanExpression(node.Expression1, script);
            }

            script.Append(" ").Append(ScriptHelper.GetComparisonLiteral(node.Token)).Append(" ");

            if (node.Expression2 is Identifier identifier2)
            {
                VisitIdentifier(identifier2, script);
            }
            else if (node.Expression2 is ScalarExpression scalar2)
            {
                VisitScalarExpression(scalar2, script);
            }
            else
            {
                VisitBooleanExpression(node.Expression2, script);
            }
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

            script.Append(name);
        }
        private void VisitScalarExpression(ScalarExpression scalar, StringBuilder script)
        {
            script.Append(scalar.Literal);
        }

        #endregion

        #endregion
    }
}