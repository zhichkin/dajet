using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class MetadataBinder
    {
        public bool TryBind(in ScriptScope scope, in MetadataCache metadata, out string error)
        {
            error = string.Empty;

            try
            {
                BindCommonTables(in scope, in metadata);

                BindSchemaTables(in scope, in metadata);
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return string.IsNullOrWhiteSpace(error);
        }
        private void ThrowBindingException(Identifier identifier)
        {
            string message = "Failed to bind " + "[" + identifier.Token + ": " + identifier.Value + "]";

            throw new InvalidOperationException(message);
        }

        private void BindCommonTables(in ScriptScope scope, in MetadataCache metadata)
        {
            foreach (ScriptScope child in scope.Children)
            {
                BindCommonTables(in child, in metadata);
            }

            if (scope.Owner is not CommonTableExpression)
            {
                return;
            }

            foreach (ScriptScope child in scope.Children)
            {
                if (child.Owner is CommonTableExpression)
                {
                    continue;
                }

                BindTables(in child, in metadata);

                BindColumns(in child, in metadata);

                BindVariables(in child, in metadata);
            }
        }
        private void BindSchemaTables(in ScriptScope scope, in MetadataCache metadata)
        {
            foreach (ScriptScope child in scope.Children)
            {
                BindSchemaTables(in child, in metadata);
            }

            if (scope.Owner is CommonTableExpression)
            {
                return;
            }

            BindTables(in scope, in metadata);

            BindColumns(in scope, in metadata);

            BindVariables(in scope, in metadata);
        }

        private void BindTables(in ScriptScope scope, in MetadataCache metadata)
        {
            foreach (SyntaxNode node in scope.Identifiers)
            {
                if (node is not Identifier identifier)
                {
                    continue; // SubqueryExpression
                }

                if (identifier.Token != TokenType.Table)
                {
                    continue;
                }

                BindTable(in scope, in identifier, in metadata);
            }
        }
        private void BindTable(in ScriptScope scope, in Identifier identifier, in MetadataCache metadata)
        {
            BindCte(in scope, in identifier);

            if (identifier.Tag != null)
            {
                return; // successful binding
            }

            MetadataObject table = null!;

            try
            {
                table = metadata.GetMetadataObject(identifier.Value);
            }
            catch
            {
                ThrowBindingException(identifier);
            }

            if (table is ApplicationObject entity)
            {
                identifier.Tag = entity;
            }
            else
            {
                ThrowBindingException(identifier);
            }
        }
        private void BindCte(in ScriptScope scope, in Identifier identifier)
        {
            ScriptScope context = scope.Ancestor<CommonTableExpression>();

            if (context != null)
            {
                // Контекст общего табличного выражения

                BindCteScoped(in context, in identifier);
            }
            else
            {
                // Корневой контекст всего выражения

                ScriptScope root = scope.Ancestor<SelectStatement>();

                if (root == null)
                {
                    root = scope.Ancestor<DeleteStatement>();
                }

                BindCteScoped(in root, in identifier);
            }
        }
        private void BindCteScoped(in ScriptScope scope, in Identifier identifier)
        {
            if (scope == null)
            {
                return;
            }

            if (scope.Owner is CommonTableExpression owner && owner.Name == identifier.Value)
            {
                identifier.Tag = owner; // bind CTE reference

                if (IsRecursiveCte(in scope, in identifier))
                {
                    owner.IsRecursive = true; // NOTE: это нужно для генератора PG SQL
                }

                return; // successful binding
            }

            foreach (ScriptScope child in scope.Children)
            {
                BindCteScoped(in child, in identifier);

                if (identifier.Tag != null)
                {
                    return; // successful binding
                }
            }
        }
        private bool IsRecursiveCte(in ScriptScope scope, in Identifier identifier)
        {
            foreach (Identifier test in scope.Identifiers)
            {
                if (test == identifier)
                {
                    return true;
                }
            }

            foreach (ScriptScope child in scope.Children)
            {
                if (child.Owner is CommonTableExpression)
                {
                    continue;
                }

                if (IsRecursiveCte(in child, in identifier))
                {
                    return true;
                }
            }

            return false;
        }
        
        private void BindColumns(in ScriptScope scope, in MetadataCache metadata)
        {
            foreach (SyntaxNode node in scope.Identifiers)
            {
                if (node is not Identifier identifier)
                {
                    continue; // SubqueryExpression
                }

                if (identifier.Token != TokenType.Column)
                {
                    continue;
                }

                BindColumn(in scope, in identifier, in metadata);

                if (identifier.Tag == null)
                {
                    ThrowBindingException(identifier);
                }
            }
        }
        private void BindColumn(in ScriptScope scope, in Identifier identifier, in MetadataCache metadata)
        {
            ScriptHelper.GetColumnNames(identifier.Value, out string tableAlias, out string columnName);

            foreach (SyntaxNode node in scope.Identifiers)
            {
                if (node is SubqueryExpression subquery && subquery.Alias == tableAlias)
                {
                    if (subquery.Expression is SelectStatement select)
                    {
                        foreach (SyntaxNode item in select.SELECT) // Привязка через синоним вложенного запроса
                        {
                            if (item is Identifier column && column.Token == TokenType.Column)
                            {
                                if (string.IsNullOrWhiteSpace(column.Alias))
                                {
                                    if (ScriptHelper.GetColumnName(column.Value) == columnName) // Привязка по имени колонки
                                    {
                                        if (column.Tag != null)
                                        {
                                            identifier.Tag = column.Tag;
                                        }

                                        return; // use already existing binding
                                    }
                                }
                                else if (column.Alias == columnName) // Привязка по синониму колонки
                                {
                                    //identifier.Tag = column.Tag;     // Проброс свойства теряет информацию о синониме колонки
                                    //identifier.Alias = column.Alias; // Важно пробросить наверх синоним колонки

                                    identifier.Tag = column; // bubble up identifier, entity property is in the Tag
                                    return; // successful binding
                                }
                            }
                        }
                    }
                    else if (subquery.Expression is TableUnionOperator union && union.Expression1 is SelectStatement unionSelect)
                    {
                        BindColumnToSelect(in unionSelect, in identifier);
                    }
                }
                else if (node is Identifier table && table.Token == TokenType.Table) // Привязка колонки по имени таблицы (синониму),
                {                                                                    // находящейся в текущей области видимости
                    if (!string.IsNullOrWhiteSpace(tableAlias) &&
                        (tableAlias.ToLowerInvariant() == "deleted" || tableAlias.ToLowerInvariant() == "inserted"))
                    {
                        BindColumnToOutput(in scope, in identifier, in metadata);
                    }
                    else if (table.Alias == tableAlias || string.IsNullOrWhiteSpace(tableAlias))
                    {
                        string alias = string.IsNullOrWhiteSpace(table.Alias) ? table.Value : table.Alias;

                        if (table.Tag is ApplicationObject entity)
                        {
                            foreach (MetadataProperty property in entity.Properties)
                            {
                                if (property.Name == columnName)
                                {
                                    List<string> fields = new();

                                    foreach (MetadataColumn field in property.Columns)
                                    {
                                        fields.Add(field.Name);
                                    }

                                    identifier.Tag = property;

                                    return; // successful binding
                                }
                            }
                        }
                        else if (table.Tag is CommonTableExpression cte)
                        {
                            if (cte.Expression is SelectStatement select)
                            {
                                BindColumnToSelect(in select, in identifier);
                            }
                            else if (cte.Expression is TableUnionOperator union && union.Expression1 is SelectStatement unionSelect)
                            {
                                BindColumnToSelect(in unionSelect, in identifier);
                            }
                        }
                    }
                    else if (string.IsNullOrWhiteSpace(table.Alias) && table.Value == tableAlias)
                    {
                        if (table.Tag is CommonTableExpression cte)
                        {
                            if (cte.Expression is SelectStatement select)
                            {
                                BindColumnToSelect(in select, in identifier);
                            }
                            else if (cte.Expression is TableUnionOperator union && union.Expression1 is SelectStatement unionSelect)
                            {
                                BindColumnToSelect(in unionSelect, in identifier);
                            }
                        }
                    }
                }
            }

            if (identifier.Tag != null)
            {
                return; // successful binding
            }

            foreach (ScriptScope child in scope.Children)
            {
                if (child.Type == ScopeType.Root)
                {
                    continue;
                }

                BindColumn(in child, in identifier, in metadata);

                if (identifier.Tag != null)
                {
                    return; // successful binding
                }
            }
        }
        private void BindColumnToSelect(in SelectStatement select, in Identifier identifier)
        {
            ScriptHelper.GetColumnNames(identifier.Value, out string _, out string columnName);

            foreach (SyntaxNode item in select.SELECT)
            {
                if (item is Identifier column && column.Token == TokenType.Column)
                {
                    if (string.IsNullOrWhiteSpace(column.Alias)) // Привязка по имени колонки
                    {
                        if (ScriptHelper.GetColumnName(column.Value) == columnName)
                        {
                            if (column.Tag != null)
                            {
                                identifier.Tag = column.Tag;
                            }

                            return; // Используем уже существующую привязку
                        }
                    }
                    else if (column.Alias == columnName) // Привязка по синониму колонки
                    {
                        //identifier.Tag = column.Tag;     // Проброс свойства теряет информацию о синониме колонки
                        //identifier.Alias = column.Alias; // Важно пробросить наверх синоним колонки

                        identifier.Tag = column; // bubble up identifier, entity property is in the Tag

                        return; // successful binding
                    }
                }
                else if (item is FunctionExpression function)
                {
                    identifier.Tag = function;
                    return; // successful binding
                }
                else if (item is CaseExpression _case)
                {
                    identifier.Tag = _case;
                    return; // successful binding
                }
            }
        }
        private void BindColumnToOutput(in ScriptScope scope, in Identifier identifier, in MetadataCache metadata)
        {
            ScriptHelper.GetColumnNames(identifier.Value, out string tableAlias, out string columnName);

            if (tableAlias.ToLowerInvariant() == "deleted")
            {
                ScriptScope ancestor = scope.Ancestor<DeleteStatement>();

                if (ancestor.Owner is DeleteStatement delete)
                {
                    if (delete.TARGET != null)
                    {
                        if (delete.TARGET.Expression is Identifier table)
                        {
                            if (table.Tag is CommonTableExpression cte)
                            {
                                if (cte.Expression is SelectStatement select)
                                {
                                    BindColumnToSelect(in select, in identifier);
                                }
                            }
                            else if (table.Tag is ApplicationObject entity)
                            {
                                // TODO
                            }
                        }
                    }
                }
            }
        }

        private void BindVariables(in ScriptScope scope, in MetadataCache metadata)
        {
            foreach (SyntaxNode node in scope.Identifiers)
            {
                if (node is not Identifier identifier)
                {
                    continue;
                }

                if (identifier.Token != TokenType.Variable)
                {
                    continue;
                }

                BindVariable(in scope, in identifier, in metadata);
            }
        }
        private void BindVariable(in ScriptScope scope, in Identifier identifier, in MetadataCache metadata)
        {
            ScriptScope root = scope.Root;

            if (root.Owner is not ScriptModel script)
            {
                throw new InvalidOperationException("Root scope is not found");
            }

            foreach (SyntaxNode node in script.Statements)
            {
                if (node is DeclareStatement declare)
                {
                    if (declare.Name.Substring(1) == identifier.Value.Substring(1)) // remove leading @ or &
                    {
                        if (ScriptHelper.IsDataType(declare.Type, out Type type))
                        {
                            if (type == typeof(Entity))
                            {
                                if (declare.Initializer is ScalarExpression scalar)
                                {
                                    identifier.Tag = Entity.Parse(scalar.Literal);
                                }
                            }
                            else
                            {
                                identifier.Tag = type;
                            }
                        }
                        else
                        {
                            MetadataObject table = metadata.GetMetadataObject(declare.Type);

                            if (table is ApplicationObject entity)
                            {
                                identifier.Tag = new Entity(entity.TypeCode, Guid.Empty);
                            }
                        }
                        break;
                    }
                }
            }

            if (identifier.Tag == null)
            {
                ThrowBindingException(identifier);
            }
        }
    }
}