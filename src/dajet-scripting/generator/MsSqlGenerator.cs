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
        protected override void Visit(in UpsertStatement node, in StringBuilder script)
        {
            if (node.Target.Binding is MetadataObject || node.Target.Binding is TemporaryTableExpression)
            {
                node.Hints = new() { "UPDLOCK", "SERIALIZABLE" };
            }

            base.Visit(in node, in script);
        }
    }
}