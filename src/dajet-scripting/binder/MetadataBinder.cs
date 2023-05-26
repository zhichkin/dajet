using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class MetadataBinder
    {
        public bool TryBind(in ScriptScope scope, in IMetadataProvider metadata, out string error)
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

        private void BindScriptScope(in ScriptScope scope, in IMetadataProvider metadata)
        {
            ScriptScope root = scope.Root;

            if (root.Owner is not ScriptModel)
            {
                throw new InvalidOperationException("Root scope is missing");
            }

            BindDataTypes(in scope, in metadata);
            BindTableVariables(in scope);
            BindVariables(in scope, in metadata);
            BindScriptScopeTables(in scope, in metadata);
        }
        private void BindDataTypes(in ScriptScope scope, in IMetadataProvider metadata)
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
        private void BindDataType(in ScriptScope scope, in TypeIdentifier identifier, in IMetadataProvider metadata)
        {
            //TODO: bind Union type for CREATE TYPE statement

            if (ScriptHelper.IsDataType(identifier.Identifier, out Type type))
            {
                // Guid, bool, decimal, DateTime, string, byte[], Entity, TypeDefinition (table)
                identifier.Binding = type;
            }
            else
            {
                MetadataObject table = metadata.GetMetadataObject(identifier.Identifier);

                if (table is TypeDefinition definition)
                {
                    identifier.Binding = definition;
                }
                else if (table is ApplicationObject entity)
                {
                    identifier.Binding = new Entity(entity.TypeCode, Guid.Empty);
                }
            }

            if (identifier.Binding == null)
            {
                ThrowBindingException(identifier.Token, identifier.Identifier);
            }
        }
        private void BindTableVariables(in ScriptScope scope)
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
                    if (declare.Type.Binding is TypeDefinition definition)
                    {
                        definition.TableName = declare.Name;
                    }
                }
            }
        }
        private void BindVariables(in ScriptScope scope, in IMetadataProvider metadata)
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
        private void BindVariable(in ScriptScope scope, in VariableReference variable, in IMetadataProvider metadata)
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
                    if (declare.Name == variable.Identifier)
                    {
                        if (declare.Type.Binding is TypeDefinition definition)
                        {
                            variable.Binding = definition;
                        }
                        else if (ScriptHelper.IsDataType(declare.Type.Identifier, out Type type))
                        {
                            if (type == typeof(Entity))
                            {
                                if (declare.Initializer is ScalarExpression scalar)
                                {
                                    variable.Binding = Entity.Parse(scalar.Literal);
                                }
                            }
                            else
                            {
                                variable.Binding = type;
                            }
                        }
                        else
                        {
                            MetadataObject table = metadata.GetMetadataObject(declare.Type.Identifier);

                            if (table is ApplicationObject entity)
                            {
                                variable.Binding = new Entity(entity.TypeCode, Guid.Empty);
                            }
                        }
                        break;
                    }
                }
            }

            if (variable.Binding == null)
            {
                ThrowBindingException(variable.Token, variable.Identifier);
            }
        }

        private void BindScriptScopeTables(in ScriptScope scope, in IMetadataProvider metadata)
        {
            foreach (ScriptScope child in scope.Children)
            {
                if (child.Owner is TableVariableExpression ||
                    child.Owner is TemporaryTableExpression)
                {
                    BindResultScope(in child, in metadata);
                }
            }
        }

        private void BindCommonScope(in ScriptScope scope, in IMetadataProvider metadata)
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
        private void BindCommonTableScope(in ScriptScope scope, in IMetadataProvider metadata)
        {
            foreach (ScriptScope child in scope.Children)
            {
                BindCommonTableScope(in child, in metadata);
            }

            BindTables(in scope, in metadata);
            BindColumns(in scope, in metadata);
        }
        
        private void BindResultScope(in ScriptScope scope, in IMetadataProvider metadata)
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
        }

        private void BindTables(in ScriptScope scope, in IMetadataProvider metadata)
        {
            foreach (SyntaxNode node in scope.Identifiers)
            {
                if (node is TableReference table)
                {
                    BindTable(in scope, in table, in metadata);
                }
            }
        }
        private void BindTable(in ScriptScope scope, in TableReference table, in IMetadataProvider metadata)
        {
            // 0. bind table to script scope tables: variable or temporary

            BindScriptTable(in scope, in table);

            if (table.Binding is not null) { return; } // successful binding

            // 1. bind table in current scope

            BindTableScoped(in scope, in table);

            if (table.Binding is not null) { return; } // successful binding

            // 2. failed to bind in current scope - bind to common table

            ScriptScope context = scope.Ancestor<CommonTableExpression>();

            if (context is null) // result context
            {
                context = scope.Ancestor<SelectStatement>();
                context ??= scope.Ancestor<UpsertStatement>();
                context ??= scope.Ancestor<InsertStatement>();
                context ??= scope.Ancestor<UpdateStatement>();
                context ??= scope.Ancestor<DeleteStatement>();
                context ??= scope.Ancestor<ConsumeStatement>();

                BindCommonTable(context, in table);
            }
            else // common table context
            {
                BindCommonTable(context, in table);
            }
            if (table.Binding is not null) { return; } // successful binding

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
                        table.Binding = derived; return; // successful binding
                    }
                }
                else if (child.Owner is CommonTableExpression common)
                {
                    if (common.Name == table.Identifier)
                    {
                        table.Binding = common; return; // successful binding
                    }
                }

                BindTableScoped(in child, in table); // go down the scope tree
            }
        }
        private void BindScriptTable(in ScriptScope scope, in TableReference table)
        {
            ScriptScope root = scope.Ancestor<ScriptModel>();

            if (root is null) { return; }

            // bind declared table variables

            foreach (SyntaxNode node in root.Identifiers)
            {
                if (node is DeclareStatement declare && declare.Name == table.Identifier)
                {
                    table.Binding = declare.Type.Binding; return; // successful binding
                }
            }

            // bind variable or temporary tables

            foreach (ScriptScope child in root.Children)
            {
                if (child.Owner is TableVariableExpression variable && variable.Name == table.Identifier)
                {
                    table.Binding = variable; return; // successful binding
                }
                else if (child.Owner is TemporaryTableExpression temporary && temporary.Name == table.Identifier)
                {
                    table.Binding = temporary; return; // successful binding
                }
            }
        }
        private void BindCommonTable(in ScriptScope scope, in TableReference table)
        {
            if (scope is null) { return; }

            if (scope.Owner is CommonTableExpression common && common.Name == table.Identifier)
            {
                table.Binding = common; return; // successful binding
            }

            foreach (ScriptScope child in scope.Children)
            {
                BindCommonTable(in child, in table);

                if (table.Binding is not null)
                {
                    break; // successful binding
                }
            }
        }
        private void BindSchemaTable(in TableReference table, in IMetadataProvider metadata)
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
                table.Binding = entity; // successful binding
            }
            else
            {
                ThrowBindingException(table.Token, table.Identifier);
            }
        }

        private void BindColumns(in ScriptScope scope, in IMetadataProvider metadata)
        {
            foreach (ScriptScope child in scope.Children)
            {
                BindColumns(in child, in metadata);
            }

            foreach (SyntaxNode node in scope.Identifiers)
            {
                if (node is ColumnReference column)
                {
                    if (!TryBindEnumValue(in column, in metadata))
                    {
                        BindColumn(in scope, in column);
                    }
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

            if (column.Binding is null)
            {
                ThrowBindingException(column.Token, column.Identifier);
            }
        }
        private bool TryBindEnumValue(in ColumnReference column, in IMetadataProvider metadata)
        {
            string[] identifiers = column.Identifier.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (identifiers is null || identifiers.Length != 3) { return false; }

            if (metadata.TryGetEnumValue(column.Identifier, out EnumValue value) && value is not null)
            {
                column.Binding = value;
                column.Token = TokenType.Enumeration;

                return true;
            }

            return false;
        }

        private bool TryGetSourceTable(in ScriptScope scope, in string identifier, out object table)
        {
            // check if it is OUTPUT clause context

            if (identifier.ToLowerInvariant() == "deleted" || identifier.ToLowerInvariant() == "inserted")
            {
                return TryGetTableScoped(in scope, in identifier, out table);
            }

            // check if this is script scope table

            if (TryGetScriptTable(in scope, in identifier, out table))
            {
                return true;
            }

            // check if this is common table context first

            ScriptScope context = scope.Ancestor<CommonTableExpression>();

            if (context is null) // statement result context
            {
                context = scope.Ancestor<SelectStatement>();
                context ??= scope.Ancestor<DeleteStatement>();
                context ??= scope.Ancestor<UpsertStatement>();
                context ??= scope.Ancestor<ConsumeStatement>();

                //NOTE: UPDATE statement columns are not searched in CTE if it is ordinary WHERE UPDATE without FROM clause.
                //If it is FROM UPDATE then such kind of columns are searched via referenced tables in the FROM clause
                //if (context is null) { context = scope.Ancestor<InsertStatement>(); }
                //if (context is null) { context = scope.Ancestor<UpdateStatement>(); }
                //if (context is null) { context = scope.Ancestor<DeleteStatement>(); }
            }

            if (TryGetCommonTable(context, in identifier, out table))
            {
                return true;
            }

            // search in the current scope 

            return TryGetTableScoped(in scope, in identifier, out table);
        }
        private bool TryGetScriptTable(in ScriptScope scope, in string identifier, out object table)
        {
            table = null;

            if (scope is null) { return false; }

            ScriptScope root = scope.Ancestor<ScriptModel>();

            if (root is null) { return false; }

            foreach (ScriptScope child in root.Children)
            {
                if (child.Owner is TableVariableExpression variable && variable.Name == identifier)
                {
                    table = variable; return true ; // success
                }
                else if (child.Owner is TemporaryTableExpression temporary && temporary.Name == identifier)
                {
                    table = temporary; return true; // success
                }
            }

            return false; // not found
        }
        private bool TryGetCommonTable(in ScriptScope scope, in string identifier, out object table)
        {
            table = null;

            if (scope is null) { return false; }

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
                    // check identifier is the keyword used in OUTPUT clause - take first available table
                    if (identifier.ToLowerInvariant() == "deleted" || identifier.ToLowerInvariant() == "inserted")
                    {
                        // TODO: find all candidate tables and warn ambiguous names
                        table = reference.Binding; return true; // success
                    }

                    if (string.IsNullOrEmpty(identifier)) // identifier is not provided - take first available table
                    {
                        // TODO: find all candidate tables and warn ambiguous names
                        table = reference.Binding; return true; // success
                    }

                    if (string.IsNullOrEmpty(reference.Alias)) // table has no alias
                    {
                        if (reference.Identifier == identifier) // match by table identifier
                        {
                            table = reference.Binding; return true; // success
                        }
                    }
                    else if (reference.Alias == identifier) // match by table alias
                    {
                        table = reference.Binding; return true; // success
                    }
                }
            }

            // identifier is not provided and current scope does not have any TableReference
            // we might be in the ORDER clause of UNION operator ... exceptional case
            if (string.IsNullOrEmpty(identifier) && scope.Owner is TableUnionOperator union)
            {
                table = union; return true; // success
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
            else if (source is TableVariableExpression variable)
            {
                BindColumn(in variable, in identifier, in column);
            }
            else if (source is TemporaryTableExpression temporary)
            {
                BindColumn(in temporary, in identifier, in column);
            }
            else if (source is TableUnionOperator union) // ORDER clause column of the UNION operator 
            {
                BindColumn(in union, in identifier, in column);
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
            else if (table.Expression is InsertStatement insert)
            {
                BindColumn(in insert, in identifier, in column);
            }
            else if (table.Expression is UpdateStatement update)
            {
                BindColumn(in update, in identifier, in column);
            }
            else if (table.Expression is DeleteStatement delete)
            {
                BindColumn(in delete, in identifier, in column);
            }
        }
        private void BindColumn(in TableVariableExpression table, in string identifier, in ColumnReference column)
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
        private void BindColumn(in TemporaryTableExpression table, in string identifier, in ColumnReference column)
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
                    column.Binding = expression; return;
                }
            }
        }
        private void BindColumn(in ApplicationObject entity, in string identifier, in ColumnReference column)
        {
            foreach (MetadataProperty property in entity.Properties)
            {
                if (property.Name == identifier)
                {
                    column.Binding = property; return;
                }
            }
        }

        private void BindColumn(in OutputClause output, in string identifier, in ColumnReference column)
        {
            if (output is null) { return; }

            string columnName = string.Empty;

            foreach (ColumnExpression expression in output.Columns)
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
                    column.Binding = expression; return; // success
                }
            }
        }
        private void BindColumn(in InsertStatement table, in string identifier, in ColumnReference column)
        {
            if (table.Output is not null) { BindColumn(table.Output, in identifier, in column); }
        }
        private void BindColumn(in UpdateStatement table, in string identifier, in ColumnReference column)
        {
            if (table.Output is not null) { BindColumn(table.Output, in identifier, in column); }
        }
        private void BindColumn(in DeleteStatement table, in string identifier, in ColumnReference column)
        {
            if (table.Output is not null) { BindColumn(table.Output, in identifier, in column); }
        }
    }
}