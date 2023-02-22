using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using System.Data;
using System.Text;
using System.Xml.Linq;

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

            script.Append(';').AppendLine();
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

            script.Append(';').AppendLine();
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

        private void ConfigureTableAlias(in SyntaxNode node)
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
                throw new InvalidOperationException("UPSERT: derived table alias is not defined.");
            }
        }
        private string[] GetInsertSelectColumnLists(in SyntaxNode target, in SyntaxNode source)
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
        private void TransformSetClause(in SyntaxNode target, in SyntaxNode source, in List<SetExpression> set_clause, in StringBuilder script)
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
        protected override void Visit(in UpsertStatement node, in StringBuilder script)
        {
            if (node.Target.Binding is CommonTableExpression)
            {
                throw new InvalidOperationException("UPSERT: computed table (cte) targeting is not allowed.");
            }

            if (node.Set is null || node.Set.Count == 0)
            {
                throw new InvalidOperationException("UPSERT: SET clause is not defined.");
            }

            if (node.Source is null)
            {
                throw new InvalidOperationException("UPSERT: FROM clause is not defined.");
            }

            ConfigureTableAlias(node.Source); // @variable and #temporary tables

            script.AppendLine();

            #region "UPDATE STATEMENT"

            if (!node.IgnoreUpdate)
            {
                if (node.CommonTables is not null)
                {
                    script.Append("WITH ");
                    Visit(node.CommonTables, in script);
                }

                script.Append("UPDATE ");
                if (!string.IsNullOrEmpty(node.Target.Alias))
                {
                    script.Append(node.Target.Alias);
                }
                else
                {
                    script.Append(node.Target.Identifier);
                }

                if (node.Target.Binding is MetadataObject ||
                    node.Target.Binding is TemporaryTableExpression)
                {
                    script.Append(" WITH (UPDLOCK, SERIALIZABLE)");
                }

                script.AppendLine().Append("SET ");
                TransformSetClause(node.Target, node.Source, node.Set, in script);
                //SetExpression set;
                //for (int i = 0; i < node.Set.Count; i++)
                //{
                //    set = node.Set[i];
                //    if (i > 0) { script.Append(","); }
                //    Visit(in set, in script);
                //}

                script.AppendLine().Append($"FROM ");
                Visit(node.Target, in script);

                script.Append($" INNER JOIN ");
                Visit(node.Source, in script);
                script.Append($" ON ");
                Visit(node.Where.Expression, in script);
                script.Append(';');
            }

            #endregion

            #region "INSERT STATEMENT"

            script.AppendLine();

            if (node.CommonTables is not null)
            {
                script.Append("WITH ");
                Visit(node.CommonTables, in script);
            }

            string[] columns = GetInsertSelectColumnLists(node.Target, node.Source);
            script.Append("INSERT INTO ");
            VisitTargetTable(node.Target, in script);
            script.Append(' ');
            script.Append('(');
            script.Append(columns[0]);
            script.AppendLine(")");
            if (node.Source is TableReference table)
            {
                script.AppendLine("SELECT");
                script.AppendLine(columns[1]);
                script.Append("FROM ");
                Visit(in table, in script);
            }
            else if (node.Source is TableExpression select)
            {
                script.AppendLine("SELECT ");
                script.AppendLine(columns[1]);
                script.Append("FROM ");
                Visit(in select, in script);
            }
            else
            {
                throw new InvalidOperationException("UPSERT: FROM clause contains invalid table source.");
            }

            script.AppendLine().Append($"WHERE NOT EXISTS (SELECT 1 FROM ");
            Visit(node.Target, in script);
            script.Append(' ');
            Visit(node.Where, in script);
            script.Append(')');
            script.Append(';');

            #endregion
        }
    }
}