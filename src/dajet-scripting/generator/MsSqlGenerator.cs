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