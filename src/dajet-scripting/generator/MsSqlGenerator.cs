using DaJet.Data;
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
                        ScriptHelper.GetColumnIdentifiers(reference.Identifier, out string tableAlias, out _);

                        if (tableAlias.ToLowerInvariant() != "deleted")
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
                // DELETE table1 FROM table1 INNER JOIN table2
                // ON table1.id = table2.id WHERE table1.col = 'test'
                Visit(node.Where, script);
            }
        }
        protected override void Visit(in OutputClause node, in StringBuilder script)
        {
            script.AppendLine().AppendLine("OUTPUT");

            for (int i = 0; i < node.Columns.Count; i++)
            {
                if (i > 0) { script.AppendLine(","); }

                Visit(node.Columns[i], in script);
            }
        }

        protected override void Visit(in ConsumeStatement node, in StringBuilder script)
        {
            SelectExpression source = TransformConsumeToSelect(in node);

            CommonTableExpression queue = new() { Name = "queue", Expression = source };

            DeleteStatement output = TransformConsumeToDelete(in node, in queue);

            Visit(in output, in script);
        }
        private SelectExpression TransformConsumeToSelect(in ConsumeStatement consume)
        {
            SelectExpression select = new()
            {
                Select = consume.Columns,
                Top = consume.Top,
                From = consume.From,
                Where = consume.Where,
                Order = consume.Order,
                Hints = "WITH (ROWLOCK, READPAST)"
            };

            return select;
        }
        private DeleteStatement TransformConsumeToDelete(in ConsumeStatement consume, in CommonTableExpression queue)
        {
            DeleteStatement delete = new()
            {
                Output = new OutputClause(),
                Target = new TableReference()
                {
                    Binding = queue,
                    Identifier = queue.Name
                },
                CommonTables = queue
            };

            foreach (ColumnExpression output in consume.Columns)
            {
                if (output.Expression is ColumnReference column)
                {
                    ColumnExpression expression = new() { Alias = output.Alias };

                    ScriptHelper.GetColumnIdentifiers(column.Identifier, out _, out string columnName);

                    ColumnReference reference = new()
                    {
                        Binding = output,
                        Identifier = "deleted." + (string.IsNullOrEmpty(output.Alias) ? columnName : output.Alias)
                    };

                    if (column.Mapping is not null)
                    {
                        reference.Mapping = new List<ColumnMap>();

                        foreach (ColumnMap map in column.Mapping)
                        {
                            reference.Mapping.Add(new ColumnMap()
                            {
                                Type = map.Type,
                                Name = "deleted." + map.Alias
                            });
                        }
                    }

                    expression.Expression = reference;

                    delete.Output.Columns.Add(expression);
                }
                else if (output.Expression is FunctionExpression function && function.Token == TokenType.DATALENGTH)
                {
                    delete.Output.Columns.Add(new ColumnExpression()
                    {
                        Expression = new ColumnReference()
                        {
                            Binding = output,
                            Identifier = "deleted." + output.Alias,
                            Mapping = new List<ColumnMap>()
                            {
                                new ColumnMap()
                                {
                                    Type = UnionTag.Integer,
                                    Name ="deleted." + output.Alias
                                }
                            }
                        }
                    });
                }
            }

            return delete;
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