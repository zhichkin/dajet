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
                BindScriptScope(in scope, in metadata);
                BindCommonScope(in scope, in metadata);
                BindResultScope(in scope, in metadata);
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return string.IsNullOrWhiteSpace(error);
        }
        private void ThrowBindingException(TokenType token, string identifier)
        {
            string message = $"Failed to bind [{token}:{identifier}]";

            throw new InvalidOperationException(message);
        }

        private void BindScriptScope(in ScriptScope scope, in MetadataCache metadata)
        {
            ScriptScope root = scope.Root;

            if (root.Owner is not ScriptModel)
            {
                throw new InvalidOperationException("Root scope is missing");
            }

            foreach (SyntaxNode node in root.Identifiers)
            {
                if (node is not TypeIdentifier identifier)
                {
                    continue;
                }

                if (ScriptHelper.IsDataType(identifier.Identifier, out Type type))
                {
                    identifier.Tag = type; // bool, decimal, DateTime, string, Union == Undefined
                }
                else
                {
                    MetadataObject table = metadata.GetMetadataObject(identifier.Identifier);

                    if (table is ApplicationObject entity)
                    {
                        identifier.Tag = new Entity(entity.TypeCode, Guid.Empty);
                    }
                }

                if (identifier.Tag == null)
                {
                    ThrowBindingException(identifier.Token, identifier.Identifier);
                }
            }
        }
        private void BindVariables(in ScriptScope scope, in MetadataCache metadata)
        {
            foreach (SyntaxNode node in scope.Identifiers)
            {
                if (node is not VariableReference identifier)
                {
                    continue;
                }

                BindVariable(in scope, in identifier, in metadata);
            }
        }
        private void BindVariable(in ScriptScope scope, in VariableReference variable, in MetadataCache metadata)
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
                    if (declare.Name.Substring(1) == variable.Identifier.Substring(1)) // remove leading @ or &
                    {
                        if (ScriptHelper.IsDataType(declare.Type.Identifier, out Type type))
                        {
                            if (type == typeof(Entity))
                            {
                                if (declare.Initializer is ScalarExpression scalar)
                                {
                                    variable.Tag = Entity.Parse(scalar.Literal);
                                }
                            }
                            else
                            {
                                variable.Tag = type;
                            }
                        }
                        else
                        {
                            MetadataObject table = metadata.GetMetadataObject(declare.Type.Identifier);

                            if (table is ApplicationObject entity)
                            {
                                variable.Tag = new Entity(entity.TypeCode, Guid.Empty);
                            }
                        }
                        break;
                    }
                }
            }

            if (variable.Tag == null)
            {
                ThrowBindingException(variable.Token, variable.Identifier);
            }
        }
        private void BindCommonScope(in ScriptScope scope, in MetadataCache metadata)
        {
            // bottom-up the scope tree - visit children first

            foreach (ScriptScope child in scope.Children)
            {
                BindCommonScope(in child, in metadata);
            }

            // the leaf scope has no children - go up again to the first cte scope to visit it

            if (scope.Owner is CommonTableExpression)
            {
                BindCommonTableScope(in scope, in metadata);
            }
        }
        private void BindCommonTableScope(in ScriptScope scope, in MetadataCache metadata)
        {
            foreach (ScriptScope child in scope.Children)
            {
                BindCommonTableScope(in child, in metadata);
            }

            BindTables(in scope, in metadata);
            BindColumns(in scope, in metadata);
            BindVariables(in scope, in metadata);
        }
        private void BindResultScope(in ScriptScope scope, in MetadataCache metadata)
        {
            // bottom-up the scope tree - visit children first

            foreach (ScriptScope child in scope.Children)
            {
                BindResultScope(in child, in metadata);
            }

            // the leaf scope has no children

            if (scope.Owner is CommonTableExpression)
            {
                return; // common table scope is binded by BindCommonScope procedure
            }

            BindTables(in scope, in metadata);
            BindColumns(in scope, in metadata);
            BindVariables(in scope, in metadata);
        }

        private void BindTables(in ScriptScope scope, in MetadataCache metadata)
        {
            foreach (SyntaxNode node in scope.Identifiers)
            {
                if (node is TableReference table)
                {
                    BindTable(in scope, in table, in metadata);
                }
            }
        }
        private void BindTable(in ScriptScope scope, in TableReference table, in MetadataCache metadata)
        {
            ScriptScope context = scope.Ancestor<CommonTableExpression>();

            // 1. bind table in current scope

            if (context is null) // result context
            {
                BindTableScoped(in scope, in table);
            }
            else // common table context
            {
                BindTableScoped(in scope, in table);
            }
            if (table.Tag is not null) { return; } // successful binding

            // 2. failed to bind in current scope - bind to common table

            if (context is null) // result context
            {
                BindCommonTable(scope.Root, in table);
            }
            else // common table context
            {
                BindCommonTable(context, in table);
            }
            if (table.Tag is not null) { return; } // successful binding

            // 3. finally bind table to the database schema object

            BindSchemaTable(in table, in metadata);
        }
        private void BindTableScoped(in ScriptScope scope, in TableReference table)
        {
            foreach (ScriptScope child in scope.Children)
            {
                if (child.Owner is SelectExpression)
                {
                    continue; // select expression scope is closed inside
                }

                if (child.Owner is TableExpression derived)
                {
                    if (derived.Alias == table.Identifier)
                    {
                        table.Tag = derived; return; // successful binding
                    }
                }
                else if (child.Owner is CommonTableExpression common)
                {
                    if (common.Name == table.Identifier)
                    {
                        table.Tag = common; return; // successful binding
                    }
                }
                
                foreach (SyntaxNode identifier in child.Identifiers)
                {
                    if (identifier is TableReference reference)
                    {
                        if (table.Identifier == reference.Alias)
                        {
                            table.Tag = reference; return; // successfull binding
                        }
                    }
                }

                BindTableScoped(in child, in table); // go down the scope tree
            }
        }
        private void BindCommonTable(in ScriptScope scope, in TableReference table)
        {
            if (scope.Owner is CommonTableExpression common && common.Name == table.Identifier)
            {
                table.Tag = common; return; // successful binding
            }

            foreach (ScriptScope child in scope.Children)
            {
                BindCommonTable(in child, in table);

                if (table.Tag != null)
                {
                    break; // successful binding
                }
            }
        }
        private void BindSchemaTable(in TableReference table, in MetadataCache metadata)
        {
            MetadataObject schema = null;

            try
            {
                schema = metadata.GetMetadataObject(table.Identifier);
            }
            catch
            {
                ThrowBindingException(table.Token, table.Identifier);
            }

            if (schema is ApplicationObject entity)
            {
                table.Tag = entity; // successful binding
            }
            else
            {
                ThrowBindingException(table.Token, table.Identifier);
            }
        }
        
        private void BindColumns(in ScriptScope scope, in MetadataCache metadata)
        {
            return;

            foreach (SyntaxNode node in scope.Identifiers)
            {
                if (node is ColumnReference identifier)
                {
                    BindColumn(in scope, in identifier, in metadata);

                    if (identifier.Tag == null)
                    {
                        ThrowBindingException(identifier.Token, identifier.Identifier);
                    }
                }
            }
        }
        private void BindColumn(in ScriptScope scope, in ColumnReference identifier, in MetadataCache metadata)
        {
            ScriptHelper.GetColumnNames(identifier.Identifier, out string tableAlias, out string columnName);

            foreach (SyntaxNode node in scope.Identifiers)
            {
                if (node is TableExpression subquery && subquery.Alias == tableAlias)
                {
                    if (subquery.Expression is SelectExpression select)
                    {
                        foreach (ColumnExpression item in select.Select) // Привязка через синоним вложенного запроса
                        {
                            if (item.Expression is ColumnReference column)
                            {
                                if (string.IsNullOrWhiteSpace(item.Alias))
                                {
                                    if (ScriptHelper.GetColumnName(column.Identifier) == columnName) // Привязка по имени колонки
                                    {
                                        if (column.Tag != null)
                                        {
                                            identifier.Tag = column.Tag;
                                        }

                                        return; // use already existing binding
                                    }
                                }
                                else if (item.Alias == columnName) // Привязка по синониму колонки
                                {
                                    //identifier.Tag = column.Tag;     // Проброс свойства теряет информацию о синониме колонки
                                    //identifier.Alias = column.Alias; // Важно пробросить наверх синоним колонки

                                    identifier.Tag = column; // bubble up identifier, entity property is in the Tag
                                    return; // successful binding
                                }
                            }
                        }
                    }
                    else if (subquery.Expression is TableUnionOperator union && union.Expression1 is SelectExpression unionSelect)
                    {
                        BindColumnToSelect(in unionSelect, in identifier);
                    }
                }
                else if (node is TableReference table) // Привязка колонки по имени таблицы (синониму),
                {                                      // находящейся в текущей области видимости
                    if (!string.IsNullOrWhiteSpace(tableAlias) &&
                        (tableAlias.ToLowerInvariant() == "deleted" || tableAlias.ToLowerInvariant() == "inserted"))
                    {
                        BindColumnToOutput(in scope, in identifier, in metadata);
                    }
                    else if (table.Alias == tableAlias || string.IsNullOrWhiteSpace(tableAlias))
                    {
                        string alias = string.IsNullOrWhiteSpace(table.Alias) ? table.Identifier : table.Alias;

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
                            if (cte.Expression is SelectExpression select)
                            {
                                BindColumnToSelect(in select, in identifier);
                            }
                            else if (cte.Expression is TableUnionOperator union && union.Expression1 is SelectExpression unionSelect)
                            {
                                BindColumnToSelect(in unionSelect, in identifier);
                            }
                        }
                    }
                    else if (string.IsNullOrWhiteSpace(table.Alias) && table.Identifier == tableAlias)
                    {
                        if (table.Tag is CommonTableExpression cte)
                        {
                            if (cte.Expression is SelectExpression select)
                            {
                                BindColumnToSelect(in select, in identifier);
                            }
                            else if (cte.Expression is TableUnionOperator union && union.Expression1 is SelectExpression unionSelect)
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
        private void BindColumnToSelect(in SelectExpression select, in ColumnReference identifier)
        {
            ScriptHelper.GetColumnNames(identifier.Identifier, out string _, out string columnName);

            foreach (ColumnExpression item in select.Select)
            {
                if (item.Expression is ColumnReference column)
                {
                    if (string.IsNullOrWhiteSpace(item.Alias)) // Привязка по имени колонки
                    {
                        if (ScriptHelper.GetColumnName(column.Identifier) == columnName)
                        {
                            if (column.Tag != null)
                            {
                                identifier.Tag = column.Tag;
                            }

                            return; // successful binding: Используем уже существующую привязку
                        }
                    }
                    else if (item.Alias == columnName) // Привязка по синониму колонки
                    {
                        identifier.Tag = column; // bubble up identifier, entity property is in the Tag

                        return; // successful binding
                    }
                }
                else if (item.Expression is FunctionExpression function)
                {
                    if (function.Alias == columnName)
                    {
                        identifier.Tag = function;
                        return; // successful binding
                    }
                }
                else if (item.Expression is CaseExpression _case)
                {
                    if (_case.Alias == columnName)
                    {
                        identifier.Tag = _case;
                        return; // successful binding
                    }
                }
                else if (item.Expression is ScalarExpression scalar)
                {
                    if (item.Alias == columnName)
                    {
                        identifier.Tag = item;
                        return; // successful binding
                    }
                }
            }
        }
        private void BindColumnToOutput(in ScriptScope scope, in ColumnReference identifier, in MetadataCache metadata)
        {
            ScriptHelper.GetColumnNames(identifier.Identifier, out string tableAlias, out string _);

            if (tableAlias.ToLowerInvariant() == "deleted")
            {
                ScriptScope ancestor = scope.Ancestor<DeleteStatement>();

                if (ancestor.Owner is DeleteStatement delete)
                {
                    if (delete.TARGET != null)
                    {
                        if (delete.TARGET.Expression is TableReference table)
                        {
                            if (table.Tag is CommonTableExpression cte)
                            {
                                if (cte.Expression is SelectExpression select)
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
    }
}