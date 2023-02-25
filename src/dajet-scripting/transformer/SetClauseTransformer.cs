using DaJet.Metadata.Model;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class SetClauseTransformer : IScriptTransformer
    {
        public void Transform(in SyntaxNode node)
        {
            if (node is not SetClause clause) { return; }

            if (clause.Parent is UpdateStatement update)
            {
                Transform(in update, in clause);
            }
        }
        private void Transform(in UpdateStatement update, in SetClause clause)
        {
            update.Source ??= TransformToTableExpression(in clause);

            TransformSetClause(in update, in clause);
        }
        private SetExpression GetSetByName(in SetClause clause, in string name)
        {
            foreach (SetExpression set in clause.Expressions)
            {
                if (set.Column.GetName() == name)
                {
                    return set;
                }
            }
            return null;
        }
        private ColumnExpression GetColumnByName(in SelectExpression select, in string name)
        {
            foreach (ColumnExpression column in select.Select)
            {
                if (column.Alias == name)
                {
                    return column;
                }
            }
            return null;
        }
        private MetadataProperty GetColumnByName(in ApplicationObject entity, in string name)
        {
            foreach (MetadataProperty property in entity.Properties)
            {
                if (property.Name == name)
                {
                    return property;
                }
            }
            return null;
        }
        private TableExpression TransformToTableExpression(in SetClause clause)
        {
            TableExpression table = new()
            {
                Alias = "source"
            };

            SelectExpression select = new();

            foreach (SetExpression set in clause.Expressions)
            {
                string columnName = set.Column.GetName();

                ColumnExpression column = new()
                {
                    Alias = columnName,
                    Expression = set.Initializer
                };
                select.Select.Add(column);

                ColumnReference initializer = new()
                {
                    Binding = column,
                    Identifier = $"source.{columnName}",
                    Mapping = new List<ColumnMap>()
                    {
                        new ColumnMap()
                        {
                            Name = $"source.{columnName}",
                            Type = DataMapper.Infer(set.Initializer).GetSingleTagOrUndefined()
                        }
                    }
                };
                set.Initializer = initializer;
            }

            table.Expression = select;

            return table;
        }
        private void TransformSetClause(in UpdateStatement update, in SetClause clause)
        {
            List<SetExpression> result = new();

            object target_table = DataMapper.GetColumnSource(update.Target);
            object source_table = DataMapper.GetColumnSource(update.Source);

            if (target_table is null)
            {
                return;
            }
            if (source_table is not SelectExpression source_select)
            {
                return;
            }

            string target_table_name = string.Empty;
            string source_table_name = string.Empty;
            if (update.Target is TableReference table1)
            {
                if (string.IsNullOrEmpty(table1.Alias))
                {
                    target_table_name = table1.Identifier;
                }
                else
                {
                    target_table_name = table1.Alias;
                }
            }
            if (update.Source is TableReference table2)
            {
                if (string.IsNullOrEmpty(table2.Alias))
                {
                    source_table_name = table2.Identifier;
                }
                else
                {
                    source_table_name = table2.Alias;
                }
            }
            else if (update.Source is TableExpression expression)
            {
                source_table_name = expression.Alias;
            }

            List<PropertyMappingRule> rules = DataMapper.CreateMappingRules(update.Target, update.Source, clause.Expressions);

            foreach (PropertyMappingRule rule in rules)
            {
                if (rule.Source is null) { continue; }

                SetExpression set = GetSetByName(in clause, rule.Target.Name);

                if (set is null) { continue; }

                object target_binding = null;
                if (target_table is ApplicationObject entity)
                {
                    target_binding = GetColumnByName(in entity, rule.Target.Name);
                }
                else if (target_table is SelectExpression target_select)
                {
                    target_binding = GetColumnByName(in target_select, rule.Target.Name);
                }
                ColumnExpression source_binding = GetColumnByName(in source_select, rule.Source.Name);

                foreach (ColumnMappingRule map in rule.Columns)
                {
                    SetExpression new_set = new();

                    ColumnReference target = new()
                    {
                        Binding = target_binding,
                        Identifier = map.Target.Name,
                        Mapping = new List<ColumnMap>()
                        {
                            new ColumnMap()
                            {
                                Name = map.Target.Name,
                                Type = map.Target.Type
                            }
                        }
                    };
                    new_set.Column = target;

                    if (map.Source is ColumnMap column)
                    {
                        ColumnReference source = new()
                        {
                            Binding = source_binding,
                            Identifier = column.Alias,
                            Mapping = new List<ColumnMap>()
                            {
                                new ColumnMap()
                                {
                                    Type = column.Type,
                                    Name = string.IsNullOrEmpty(source_table_name)
                                    ? column.Alias
                                    : $"{source_table_name}.{column.Alias}"
                                }
                            }
                        };
                        new_set.Initializer = source;
                    }
                    else if (map.Source is ScalarExpression scalar)
                    {
                        new_set.Initializer = scalar;
                    }

                    result.Add(new_set);
                }
            }

            clause.Expressions = result;
        }
    }
}