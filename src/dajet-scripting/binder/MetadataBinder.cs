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
            string message = $"Failed to bind [{token}: {identifier}]";

            throw new InvalidOperationException(message);
        }

        private void BindScriptScope(in ScriptScope scope, in MetadataCache metadata)
        {
            ScriptScope root = scope.Root;

            if (root.Owner is not ScriptModel)
            {
                throw new InvalidOperationException("Root scope is missing");
            }

            BindDataTypes(in scope, in metadata);
            BindVariables(in scope, in metadata);
        }
        private void BindDataTypes(in ScriptScope scope, in MetadataCache metadata)
        {
            foreach (SyntaxNode node in scope.Identifiers)
            {
                if (node is not TypeIdentifier identifier)
                {
                    continue;
                }

                BindDataType(in scope, in identifier, in metadata);

                
            }
        }
        private void BindDataType(in ScriptScope scope, in TypeIdentifier identifier, in MetadataCache metadata)
        {
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
                throw new InvalidOperationException("Root scope is missing");
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
            BindColumns(in scope);
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
            BindColumns(in scope);
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
            // 1. bind table in current scope

            BindTableScoped(in scope, in table);

            if (table.Tag is not null) { return; } // successful binding

            // 2. failed to bind in current scope - bind to common table

            ScriptScope context = scope.Ancestor<CommonTableExpression>();

            if (context is null) // result context
            {
                context = scope.Ancestor<SelectStatement>();

                BindCommonTable(context, in table);
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
                    continue; // select expression scope is closed from outside
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

        private void BindColumns(in ScriptScope scope)
        {
            foreach (ScriptScope child in scope.Children)
            {
                BindColumns(in child);
            }

            foreach (SyntaxNode node in scope.Identifiers)
            {
                if (node is ColumnReference column)
                {
                    BindColumn(in scope, in column);
                }
            }
        }
        private void BindColumn(in ScriptScope scope, in ColumnReference column)
        {
            ScriptHelper.GetColumnIdentifiers(column.Identifier, out string tableAlias, out string columnName);

            if (!TryGetSourceTable(in scope, in tableAlias, out object table))
            {
                ThrowBindingException(column.Token, column.Identifier);
            }

            BindColumn(in table, in columnName, in column);

            if (column.Tag is null)
            {
                ThrowBindingException(column.Token, column.Identifier);
            }
        }

        private bool TryGetSourceTable(in ScriptScope scope, in string identifier, out object table)
        {
            // check if this is common table first

            ScriptScope context = scope.Ancestor<CommonTableExpression>();

            if (context is null) // general result context
            {
                context = scope.Ancestor<SelectStatement>(); 
            }

            if (TryGetCommonTable(context, in identifier, out table))
            {
                return true;
            }

            // search in the current scope 

            return TryGetTableScoped(in scope, in identifier, out table);
        }
        private bool TryGetCommonTable(in ScriptScope scope, in string identifier, out object table)
        {
            table = null;

            if (scope.Owner is CommonTableExpression common && common.Name == identifier)
            {
                table = common; return true; // success
            }

            foreach (ScriptScope child in scope.Children)
            {
                if (TryGetCommonTable(in child, in identifier, out table))
                {
                    return true; // success
                }
            }

            return false; // not found
        }
        private bool TryGetTableScoped(in ScriptScope scope, in string identifier, out object table)
        {
            table = null;

            // search in the current scope first

            foreach (SyntaxNode node in scope.Identifiers)
            {
                if (node is TableReference reference)
                {
                    if (string.IsNullOrEmpty(identifier)) // identifier is not provided - take first available table
                    {
                        table = reference.Tag; return true; // success
                    }

                    if (string.IsNullOrEmpty(reference.Alias)) // table has no alias
                    {
                        if (reference.Identifier == identifier) // match by table identifier
                        {
                            table = reference.Tag; return true; // success
                        }
                    }
                    else if (reference.Alias == identifier) // match by table alias
                    {
                        table = reference.Tag; return true; // success
                    }
                }
            }

            // continue to search down the scope tree

            foreach (ScriptScope child in scope.Children)
            {
                if (child.Owner is SelectExpression ||
                    child.Owner is TableUnionOperator ||
                    child.Owner is CommonTableExpression) // common table is searched first (see TryGetSourceTable)
                {
                    continue; // the scope is closed from outside
                }

                if (child.Owner is TableExpression derived && derived.Alias == identifier)
                {
                    table = derived; return true; // success
                }

                // go recursively down the scope tree

                if (TryGetTableScoped(in child, in identifier, out table))
                {
                    return true; // success
                }
            }

            // not found

            return false;
        }

        private void BindColumn(in object source, in string identifier, in ColumnReference column)
        {
            if (source is CommonTableExpression common)
            {
                BindColumn(in common, in identifier, in column);
            }
            else if (source is ApplicationObject entity)
            {
                BindColumn(in entity, in identifier, in column);
            }
            else if (source is TableExpression derived)
            {
                BindColumn(in derived, in identifier, in column);
            }
        }
        private void BindColumn(in TableExpression table, in string identifier, in ColumnReference column)
        {
            if (table.Expression is SelectExpression select)
            {
                BindColumn(in select, in identifier, in column);
            }
            else if (table.Expression is TableUnionOperator union)
            {
                BindColumn(in union, in identifier, in column);
            }
        }
        private void BindColumn(in TableUnionOperator union, in string identifier, in ColumnReference column)
        {
            if (union.Expression1 is SelectExpression select1)
            {
                BindColumn(in select1, in identifier, in column);
            }
            else if (union.Expression2 is SelectExpression select2)
            {
                BindColumn(in select2, in identifier, in column);
            }
        }
        private void BindColumn(in CommonTableExpression table, in string identifier, in ColumnReference column)
        {
            if (table.Expression is SelectExpression select)
            {
                BindColumn(in select, in identifier, in column);
            }
            else if (table.Expression is TableUnionOperator union)
            {
                BindColumn(in union, in identifier, in column);
            }
        }
        private void BindColumn(in SelectExpression table, in string identifier, in ColumnReference column)
        {
            string columnName = string.Empty;

            foreach (ColumnExpression expression in table.Select)
            {
                if (!string.IsNullOrEmpty(expression.Alias))
                {
                    columnName = expression.Alias;
                }
                else if (expression.Expression is ColumnReference reference)
                {
                    ScriptHelper.GetColumnIdentifiers(reference.Identifier, out string _, out columnName);
                }

                if (columnName == identifier)
                {
                    column.Tag = expression; return;
                }
            }
        }
        private void BindColumn(in ApplicationObject entity, in string identifier, in ColumnReference column)
        {
            foreach (MetadataProperty property in entity.Properties)
            {
                if (property.Name == identifier)
                {
                    column.Tag = property; return;
                }
            }
        }
    }
}