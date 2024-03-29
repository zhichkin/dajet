﻿using DaJet.Data;
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
            else if (clause.Parent is UpsertStatement upsert)
            {
                Transform(in upsert, in clause);
            }
        }
        private void Transform(in UpdateStatement update, in SetClause clause)
        {
            update.Source ??= TransformToTableExpression(in clause);

            TransformSetClause(in update, in clause);
        }
        private void Transform(in UpsertStatement upsert, in SetClause clause)
        {
            TransformSetClause(in upsert, in clause);
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
            foreach (ColumnExpression column in select.Columns)
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
                select.Columns.Add(column);

                ColumnReference initializer = new()
                {
                    Binding = column,
                    Identifier = $"source.{columnName}",
                    Mapping = new List<ColumnMapper>()
                    {
                        new ColumnMapper()
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
                        Mapping = new List<ColumnMapper>()
                        {
                            new ColumnMapper()
                            {
                                Name = map.Target.Name,
                                Type = map.Target.Type
                            }
                        }
                    };
                    new_set.Column = target;

                    if (map.Source is ColumnMapper column)
                    {
                        ColumnReference source = new()
                        {
                            Binding = source_binding,
                            Identifier = column.Alias,
                            Mapping = new List<ColumnMapper>()
                            {
                                new ColumnMapper()
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
        private void TransformSetClause(in UpsertStatement upsert, in SetClause clause)
        {
            List<SetExpression> result = new();

            object target_table = DataMapper.GetColumnSource(upsert.Target);
            object source_table = DataMapper.GetColumnSource(upsert.Source);

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
            if (upsert.Target is TableReference table1)
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
            if (upsert.Source is TableReference table2)
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
            else if (upsert.Source is TableExpression expression)
            {
                source_table_name = expression.Alias;
            }

            List<PropertyMappingRule> rules = DataMapper.CreateMappingRules(upsert.Target, upsert.Source, clause.Expressions);

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
                        Mapping = new List<ColumnMapper>()
                        {
                            new ColumnMapper()
                            {
                                Name = map.Target.Name,
                                Type = map.Target.Type
                            }
                        }
                    };
                    new_set.Column = target;

                    if (map.Source is ColumnMapper column)
                    {
                        ColumnReference source = new()
                        {
                            Binding = source_binding,
                            Identifier = column.Alias,
                            Mapping = new List<ColumnMapper>()
                            {
                                new ColumnMapper()
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