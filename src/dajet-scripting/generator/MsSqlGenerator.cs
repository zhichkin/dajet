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

        protected override void Visit(in DeleteStatement node, in StringBuilder script)
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

            script.Append("DELETE ");

            VisitTargetTable(node.Target, in script);

            if (node.Output is not null)
            {
                foreach (ColumnExpression column in node.Output.Columns)
                {
                    if (column.Expression is ColumnReference reference)
                    {
                        ScriptHelper.GetColumnIdentifiers(reference.Identifier, out _, out string columnName);

                        if (!columnName.ToLowerInvariant().StartsWith("deleted"))
                        {
                            reference.Identifier = "deleted." + reference.Identifier;

                            if (reference.Mapping is not null)
                            {
                                foreach (ColumnMap map in reference.Mapping)
                                {
                                    map.Name = "deleted." + map.Name;
                                }
                            }
                        }
                    }
                }

                Visit(node.Output, script);
            }

            if (node.Where is not null)
            {
                // TODO: transform WHERE into FROM ... INNER JOIN if another tables are referenced
                Visit(node.Where, script);
            }
        }
        protected override void Visit(in OutputClause node, in StringBuilder script)
        {
            script.AppendLine().AppendLine("OUTPUT");

            for (int i = 0; i < node.Columns.Count; i++)
            {
                if (i > 0) { script.Append(", "); }

                Visit(node.Columns[i], in script);
            }
        }

        protected override void Visit(in ConsumeStatement node, in StringBuilder script)
        {
            script.AppendLine("WITH queue AS").Append("(SELECT");

            if (node.Top is not null) { Visit(node.Top, in script); }

            script.AppendLine();

            for (int i = 0; i < node.Columns.Count; i++)
            {
                if (i > 0) { script.AppendLine(","); }

                Visit(node.Columns[i], in script);
            }

            if (node.From is not null) { Visit(node.From, in script); }
            script.Append(" WITH (ROWLOCK, READPAST)");
            if (node.Where is not null) { Visit(node.Where, in script); }
            if (node.Order is not null) { Visit(node.Order, in script); }

            script.AppendLine(")").AppendLine("DELETE queue OUTPUT");

            for (int i = 0; i < node.Columns.Count; i++)
            {
                if (i > 0) { script.AppendLine(","); }

                ColumnExpression output = node.Columns[i];

                if (output.Expression is ColumnReference column)
                {
                    TransformColumnReference(in column);
                }
                else if (output.Expression is FunctionExpression function)
                {
                    TransformColumnExpression(in output, in function);
                }
                
                Visit(in output, in script);
            }
        }
        private void TransformColumnReference(in ColumnReference column)
        {
            if (column.Mapping is not null)
            {
                foreach (ColumnMap map in column.Mapping)
                {
                    map.Name = "deleted." + map.Alias;
                    map.Alias = string.Empty;
                }
            }
        }
        private void TransformColumnExpression(in ColumnExpression column, in FunctionExpression function)
        {
            if (function.Name.ToUpperInvariant() != "DATALENGTH") { return; }

            column.Expression = new ColumnReference()
            {
                Binding = new ColumnExpression()
                {
                    Expression = function
                },
                Identifier = "deleted." + column.Alias,
                Mapping = new List<ColumnMap>()
                {
                    new ColumnMap() { Name = "deleted." + column.Alias }
                }
            };
        }
    }
}

// Шаблон запроса на деструктивное чтение для Microsoft SQL Server
//WITH queue AS
//(SELECT TOP (@MessageCount)
//  МоментВремени, Идентификатор, ДатаВремя,
//  Отправитель, Получатели, Заголовки,
//  ТипОперации, ТипСообщения, ТелоСообщения
//FROM
//  {TABLE_NAME} WITH (ROWLOCK, READPAST)
//ORDER BY
//  МоментВремени ASC,
//  Идентификатор ASC
//)
//DELETE queue OUTPUT
//  deleted.МоментВремени, deleted.Идентификатор, deleted.ДатаВремя,
//  deleted.Отправитель, deleted.Получатели, deleted.Заголовки,
//  deleted.ТипОперации, deleted.ТипСообщения, deleted.ТелоСообщения
//;