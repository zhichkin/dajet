using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class MetadataBinder
    {
        private ScriptScope _scope;
        private List<string> _errors;
        private IMetadataProvider _schema;
        public bool TryBind(in SyntaxNode node, in IMetadataProvider schema, out ScriptScope scope, out List<string> errors)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
            
            _errors = new List<string>();

            _scope = new ScriptScope() { Owner = node };

            try
            {
                Bind(in node);
            }
            catch (Exception exception)
            {
                _errors.Add(ExceptionHelper.GetErrorMessage(exception));
            }

            scope = _scope;
            errors = _errors;

            _scope = null;
            _errors = null;

            return (errors.Count == 0);
        }
        private void Bind(in SyntaxNode node)
        {
            if (node is null) { return; }
            else if (node is ScriptModel script) { Bind(in script); }
            else if (node is OutputClause output) { Bind(in output); }
            else if (node is InsertStatement insert) { Bind(in insert); }
            else if (node is UpdateStatement update) { Bind(in update); }
            else if (node is UpsertStatement upsert) { Bind(in upsert); }
            else if (node is DeleteStatement delete) { Bind(in delete); }
            else if (node is ValuesExpression values) { Bind(in values); }
            else if (node is SetClause set_clause) { Bind(in set_clause); }
            else if (node is SetExpression set_expression) { Bind(in set_expression); }
            else if (node is DeclareStatement declare) { Bind(in declare); }
            else if (node is TypeIdentifier type) { Bind(in type); }
            else if (node is VariableReference variable) { Bind(in variable); }
            else if (node is MemberAccessExpression member) { Bind(in member); }
            else if (node is GroupOperator group) { Bind(in group); }
            else if (node is UnaryOperator unary) { Bind(in unary); }
            else if (node is BinaryOperator binary) { Bind(in binary); }
            else if (node is MultiplyOperator multiply) { Bind(in multiply); }
            else if (node is AdditionOperator addition) { Bind(in addition); }
            else if (node is ComparisonOperator comparison) { Bind(in comparison); }
            else if (node is CaseExpression case_when_then_else) { Bind(in case_when_then_else); }
            else if (node is WhenClause when) { Bind(in when); }
            else if (node is FunctionExpression function) { Bind(in function); }
            else if (node is OverClause over) { Bind(in over); }
            else if (node is PartitionClause partition) { Bind(in partition); }
            else if (node is WindowFrame frame) { Bind(in frame); }
            else if (node is FromClause from) { Bind(in from); }
            else if (node is GroupClause group_by) { Bind(in group_by); }
            else if (node is HavingClause having) { Bind(in having); }
            else if (node is IntoClause into) { Bind(in into); }
            else if (node is OnClause join_on) { Bind(in join_on); }
            else if (node is OrderClause order_by) { Bind(in order_by); }
            else if (node is OrderExpression order_expression) { Bind(in order_expression); }
            else if (node is TopClause top) { Bind(in top); }
            else if (node is WhereClause where) { Bind(in where); }
            else if (node is ColumnExpression column) { Bind(in column); }
            else if (node is ColumnReference reference) { Bind(in reference); }
            else if (node is CommonTableExpression cte) { Bind(in cte); }
            else if (node is SelectExpression select) { Bind(in select); }
            else if (node is SelectStatement select_statement) { Bind(in select_statement); }
            else if (node is StarExpression star) { Bind(in star); }
            else if (node is TableExpression derived) { Bind(in derived); }
            else if (node is TableJoinOperator join) { Bind(in join); }
            else if (node is TableReference table) { Bind(in table); }
            else if (node is TableVariableExpression table_variable) { Bind(in table_variable); }
            else if (node is TemporaryTableExpression temporary_table) { Bind(in temporary_table); }
            else if (node is TableUnionOperator union) { Bind(in union); }
            else if (node is ConsumeStatement consume_statement) { Bind(in consume_statement); }
            else if (node is ImportStatement import_statement) { Bind(in import_statement); }
        }
        private void RegisterBindingError(TokenType token, string identifier)
        {
            _errors.Add($"Failed to bind [{token}: {identifier}]");
        }

        #region "GLOBAL SCOPE BINDING"
        private void Bind(in ScriptModel node)
        {
            _scope ??= new ScriptScope() { Owner = node };

            foreach (SyntaxNode statement in node.Statements)
            {
                Bind(in statement);
            }
        }
        private void Bind(in TypeIdentifier node)
        {
            //TODO: bind "union" type identifier

            if (ParserHelper.IsDataType(node.Identifier, out Type type))
            {
                //NOTE: bool, decimal, DateTime, string, byte[], Guid, Entity, Union, Array, object

                if (type == typeof(Array))
                {
                    node.Token = TokenType.Array;
                }
                else if (type == typeof(object))
                {
                    node.Token = TokenType.Object;
                }

                node.Binding = type;
            }
            else // EntityDefinition (table)
            {
                MetadataObject table = _schema.GetMetadataObject(node.Identifier);

                if (table is EntityDefinition definition)
                {
                    node.Binding = definition; // UDT (user-defined type)
                }
                else if (table is ApplicationObject entity)
                {
                    node.Binding = new Entity(entity.TypeCode, Guid.Empty);
                }
            }

            if (node.Binding is null)
            {
                RegisterBindingError(node.Token, node.Identifier);
            }
        }
        private void Bind(in DeclareStatement node)
        {
            if (node.Type is null) { return; }

            Bind(node.Type);

            if (node.Type.Binding is EntityDefinition definition)
            {
                definition.TableName = node.Name; // user-defined type (table-valued parameter)
            }
            else if (node.Type.Binding is Type type)
            {
                if (type == typeof(Entity)) // DECLARE @Ссылка entity
                {
                    if (node.Initializer is ScalarExpression scalar) // DECLARE @Ссылка entity = {code:guid}
                    {
                        if (Entity.TryParse(scalar.Literal, out Entity entity))
                        {
                            node.Type.Binding = entity;
                        }
                    }
                    else if (node.Initializer is SelectExpression select) // DECLARE @Ссылка entity = SELECT Ссылка FROM ... WHERE ...
                    {
                        Bind(in select);

                        if (select.Columns.Count > 0 &&
                            select.Columns[0].Expression is ColumnReference column &&
                            column.Binding is MetadataProperty property &&
                            !property.PropertyType.IsMultipleType &&
                            property.PropertyType.CanBeReference &&
                            property.PropertyType.TypeCode > 0)
                        {
                            node.Type.Binding = new Entity(property.PropertyType.TypeCode, Guid.Empty);
                        }
                        else
                        {
                            RegisterBindingError(node.Token, node.Name);
                        }
                    }
                }
                else if (node.Initializer is SelectExpression select) // all other types of variables
                {
                    Bind(in select);
                }
            }

            // join current scope

            if (node.Type.Token == TokenType.Array || node.Type.Token == TokenType.Object)
            {
                _scope.Variables.Add(node.Name, node.Type);
            }
            else
            {
                _scope.Variables.Add(node.Name, node.Type.Binding);
            }
        }
        private void Bind(in VariableReference node)
        {
            node.Binding = _scope.GetVariableBinding(node.Identifier);

            if (node.Binding is null)
            {
                RegisterBindingError(node.Token, node.Identifier);
            }
        }
        private void Bind(in MemberAccessExpression node)
        {
            string[] identifier = node.Identifier.Split('.');
            
            string target = identifier[0];
            string member = identifier[1];

            object binding = _scope.GetVariableBinding(target);

            if (binding is TypeIdentifier type && type.Binding is List<ColumnExpression> columns)
            {
                for (int i = 0; i < columns.Count; i++)
                {
                    if (DataMapper.TryMap(columns[i], out string name, out UnionType union))
                    {
                        if (name == member)
                        {
                            node.Binding = UnionType.MapToType(in union);
                            break;
                        }
                    }
                }
            }

            if (node.Binding is null)
            {
                RegisterBindingError(node.Token, node.Identifier);
            }
        }
        private void Bind(in TableVariableExpression node)
        {
            Bind(node.Expression);

            _scope.Tables.Add(node.Name, node); // join script global scope
        }
        private void Bind(in TemporaryTableExpression node)
        {
            Bind(node.Expression);

            _scope.Tables.Add(node.Name, node); // join script global scope
        }
        private void Bind(in OutputClause node)
        {
            if (node is null) { return; }

            for (int i = 0; i < node.Columns.Count; i++)
            {
                Bind(node.Columns[i]); //NOTE: use of special key words inserted and deleted
            }

            if (node.Into is not null) { Bind(node.Into); }
        }
        private void Bind(in IntoClause node)
        {
            //NOTE: INTO columns are derived from the host SELECT expression
            //NOTE: INTO columns are bound already !!!

            if (node.Table is not null)
            {
                CreateTableVariable(in node);
            }
            else
            {
                Bind(node.Value); //NOTE: bind variable data type: Array or object

                if (node.Value.Binding is TypeIdentifier type) // see DeclareStatement binding
                {
                    type.Binding = node.Columns; //NOTE: schema definition
                }
            }
        }
        private void CreateTableVariable(in IntoClause node)
        {
            SyntaxNode table;

            if (_scope.Ancestor<ConsumeStatement>() is not null)
            {
                table = new TableVariableExpression() // MS SQL Server feature
                {
                    Name = node.Table.Identifier,
                    Expression = new SelectExpression()
                    {
                        Columns = node.Columns,
                        From = new FromClause()
                        {
                            Expression = node.Table
                        }
                    }
                };
            }
            else
            {
                table = new TemporaryTableExpression()
                {
                    Name = node.Table.Identifier,
                    Expression = new SelectExpression()
                    {
                        Columns = node.Columns,
                        From = new FromClause()
                        {
                            Expression = node.Table
                        }
                    }
                };
            }

            node.Table.Binding = table;

            ScriptScope root = _scope.GetRoot();

            root.Tables.Add(node.Table.Identifier, table);
        }
        #endregion

        #region "SELECT AND TABLE BINDING"
        private void Bind(in SelectStatement node)
        {
            _scope = _scope.OpenScope(node);

            if (node.CommonTables is not null)
            {
                Bind(node.CommonTables);
            }

            Bind(node.Expression); //NOTE: SelectExpression | TableUnionOperator

            _scope = _scope.CloseScope();
        }
        private void Bind(in CommonTableExpression node)
        {
            //NOTE: common table expression can be recursive and reference itself

            if (node.Next is not null)
            {
                Bind(node.Next);
            }

            _scope.Tables.Add(node.Name, node); // join current statement scope

            Bind(node.Expression); //NOTE: { SelectExpression | TableUnionOperator | INSERT | UPDATE | DELETE }
        }
        private void Bind(in FromClause node) { Bind(node.Expression); }
        private void Bind(in SelectExpression node)
        {
            _scope = _scope.OpenScope(node);

            if (node.From is not null) { Bind(node.From); }
            
            for (int i = 0; i < node.Columns.Count; i++)
            {
                Bind(node.Columns[i]);
            }

            if (node.Top is not null) { Bind(node.Top); }
            if (node.Where is not null) { Bind(node.Where); }
            if (node.Order is not null) { Bind(node.Order); }
            if (node.Group is not null) { Bind(node.Group); }
            if (node.Having is not null) { Bind(node.Having); }

            if (node.Into is not null) { Bind(node.Into); }

            _scope = _scope.CloseScope();
        }
        private void Bind(in TableExpression node) //NOTE: this is subquery
        {
            Bind(node.Expression); //NOTE: SelectExpression - opens new scope inside current

            if (!string.IsNullOrWhiteSpace(node.Alias))
            {
                _scope.Aliases.Add(node.Alias, node); // join current SelectExpression scope
            }
        }
        private void Bind(in TableJoinOperator node)
        {
            Bind(node.Expression1); //NOTE: { TableReference | TableExpression | TableJoinOperator }
            Bind(node.Expression2);
            if (node.On is not null) { Bind(node.On); }
        }
        private void Bind(in TableUnionOperator node)
        {
            if (node.Expression1 is SelectExpression select1)
            {
                Bind(in select1);
            }
            else if (node.Expression1 is TableUnionOperator union1)
            {
                Bind(in union1);
            }

            if (node.Expression2 is SelectExpression select2)
            {
                Bind(in select2);
            }
            else if (node.Expression2 is TableUnionOperator union2)
            {
                Bind(in union2);
            }

            if (node.Order is OrderClause order) // root union operator
            {
                for (int i = 0; i < order.Expressions.Count; i++)
                {
                    if (order.Expressions[i].Expression is ColumnReference column)
                    {
                        ParserHelper.GetColumnIdentifiers(column.Identifier, out _, out string columnName);

                        BindColumn(in node, in columnName, in column);

                        if (column.Binding is null)
                        {
                            RegisterBindingError(column.Token, column.Identifier);
                        }
                    }
                }

                if (order.Offset is not null)
                {
                    Bind(order.Offset);

                    if (order.Fetch is not null)
                    {
                        Bind(order.Fetch);
                    }
                }
            }
        }
        private void Bind(in TableReference node)
        {
            // 1. try bind common table expression, temporary table or table variable
            node.Binding = _scope.GetTableBinding(node.Identifier);

            // 2. try bind user-defined type (table-valued parameter)
            // see DeclareStatement binding to EntityDefinition
            if (node.Binding is null)
            {
                node.Binding = _scope.GetVariableBinding(node.Identifier);
            }

            // 3. try bind database schema table
            if (node.Binding is null)
            {
                MetadataObject schema = _schema.GetMetadataObject(node.Identifier);

                if (schema is ApplicationObject entity)
                {
                    node.Binding = entity; // successful binding
                }
            }

            if (node.Binding is null)
            {
                RegisterBindingError(node.Token, node.Identifier);
            }
            else // successful binding
            {
                if (!string.IsNullOrWhiteSpace(node.Alias))
                {
                    _scope.Aliases.Add(node.Alias, node.Binding); // join current SelectExpression scope
                }
                else
                {
                    _scope.Aliases.Add(node.Identifier, node.Binding);
                }
            }
        }
        #endregion

        #region "COLUMN BINDING"
        private void Bind(in StarExpression node) { /* TODO: implement transformer into column expressions */ }
        private void Bind(in ColumnExpression node)
        {
            Bind(node.Expression);
        }
        private void Bind(in ColumnReference node)
        {
            if (!TryBindEnumValue(in node))
            {
                BindColumn(in node);
            }

            if (node.Binding is null)
            {
                RegisterBindingError(node.Token, node.Identifier);
            }
            else // successful binding
            {
                //TODO: find all ambiguous names and report error
                _ = _scope.Columns.TryAdd(node.Identifier, node.Binding);
            }
        }
        private bool TryBindEnumValue(in ColumnReference column)
        {
            string[] identifiers = column.Identifier.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (identifiers is null || identifiers.Length != 3) { return false; }

            if (_schema.TryGetEnumValue(column.Identifier, out EnumValue value) && value is not null)
            {
                column.Binding = value;
                column.Token = TokenType.Enumeration;

                return true;
            }

            return false;
        }
        private void BindColumn(in ColumnReference column)
        {
            ParserHelper.GetColumnIdentifiers(column.Identifier, out string tableAlias, out string columnName);

            if (_scope.TryGetTableByAlias(in tableAlias, out object table))
            {
                BindColumn(in table, in columnName, in column);
            }
            
            if (column.Binding is null)
            {
                RegisterBindingError(column.Token, column.Identifier);
            }
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

            foreach (ColumnExpression expression in table.Columns)
            {
                if (!string.IsNullOrEmpty(expression.Alias))
                {
                    columnName = expression.Alias;
                }
                else if (expression.Expression is ColumnReference reference)
                {
                    ParserHelper.GetColumnIdentifiers(reference.Identifier, out string _, out columnName);
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
                    ParserHelper.GetColumnIdentifiers(reference.Identifier, out string _, out columnName);
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
        #endregion

        #region "CLAUSE AND OPERATOR BINDING"
        private void Bind(in TopClause node)
        {
            Bind(node.Expression);
        }
        private void Bind(in WhereClause node)
        {
            Bind(node.Expression);
        }
        private void Bind(in GroupClause node)
        {
            for (int i = 0; i < node.Expressions.Count; i++)
            {
                Bind(node.Expressions[i]);
            }
        }
        private void Bind(in HavingClause node)
        {
            Bind(node.Expression);
        }
        private void Bind(in OnClause node)
        {
            Bind(node.Expression);
        }
        private void Bind(in OrderClause node)
        {
            for (int i = 0; i < node.Expressions.Count; i++)
            {
                Bind(node.Expressions[i]);
            }

            if (node.Offset is not null)
            {
                Bind(node.Offset);

                if (node.Fetch is not null)
                {
                    Bind(node.Fetch);
                }
            }
        }
        private void Bind(in OrderExpression node)
        {
            Bind(node.Expression);
        }
        private void Bind(in GroupOperator node)
        {
            Bind(node.Expression);
        }
        private void Bind(in UnaryOperator node)
        {
            Bind(node.Expression);
        }
        private void Bind(in BinaryOperator node)
        {
            Bind(node.Expression1);
            Bind(node.Expression2);
        }
        private void Bind(in AdditionOperator node)
        {
            Bind(node.Expression1);
            Bind(node.Expression2);
        }
        private void Bind(in MultiplyOperator node)
        {
            Bind(node.Expression1);
            Bind(node.Expression2);
        }
        private void Bind(in ComparisonOperator node)
        {
            Bind(node.Expression1);
            Bind(node.Expression2);
        }
        private void Bind(in CaseExpression node)
        {
            foreach (WhenClause when in node.CASE)
            {
                Bind(in when);
            }

            if (node.ELSE is not null)
            {
                Bind(node.ELSE);
            }
        }
        private void Bind(in WhenClause node)
        {
            Bind(node.WHEN);
            Bind(node.THEN);
        }
        private void Bind(in FunctionExpression node)
        {
            for (int i = 0; i < node.Parameters.Count; i++)
            {
                Bind(node.Parameters[i]);
            }

            if (node.Over is not null)
            {
                Bind(node.Over);
            }
        }
        private void Bind(in OverClause node)
        {
            if (node.Partition is not null)
            {
                Bind(node.Partition);
            }
            if (node.Order is not null)
            {
                Bind(node.Order);
            }
            if (node.Preceding is not null || node.Following is not null)
            {
                if (node.Preceding is not null && node.Following is not null)
                {
                    Bind(node.Preceding);
                    Bind(node.Following);
                }
                else if (node.Preceding is not null)
                {
                    Bind(node.Preceding);
                }
            }
        }
        private void Bind(in WindowFrame node) { }
        private void Bind(in PartitionClause node)
        {
            for (int i = 0; i < node.Columns.Count; i++)
            {
                Bind(node.Columns[i]);
            }
        }
        #endregion

        #region "DML STATEMENT BINDING"
        private void Bind(in ConsumeStatement node)
        {
            _scope = _scope.OpenScope(node);

            if (node.From is not null) { Bind(node.From); }

            for (int i = 0; i < node.Columns.Count; i++)
            {
                Bind(node.Columns[i]);
            }

            if (node.Top is not null) { Bind(node.Top); }
            if (node.Where is not null) { Bind(node.Where); }
            if (node.Order is not null) { Bind(node.Order); }

            if (node.Into is not null) { Bind(node.Into); }

            _scope = _scope.CloseScope();
        }
        private void Bind(in ImportStatement node)
        {
            _scope = _scope.OpenScope(node);

            foreach (VariableReference variable in node.Target)
            {
                Bind(in variable);
            }

            _scope = _scope.CloseScope();
        }
        private void Bind(in DeleteStatement node)
        {
            _scope = _scope.OpenScope(node);

            if (node.CommonTables is not null)
            {
                Bind(node.CommonTables); //NOTE: populates Tables list of the scope
            }

            if (node.Target is not null) { Bind(node.Target); } //NOTE: populates Aliases list of the scope
            if (node.Output is not null) { Bind(node.Output); }
            if (node.Where is not null) { Bind(node.Where); }

            //FIXME: binding columns referencing CTE fails (lookup is done on table aliases)

            _scope = _scope.CloseScope();
        }
        private void Bind(in InsertStatement node)
        {
            _scope = _scope.OpenScope(node);

            if (node.CommonTables is not null)
            {
                Bind(node.CommonTables); //NOTE: populates Tables list of the scope
            }

            if (node.Target is not null) { Bind(node.Target); } //NOTE: populates Aliases list of the scope
            if (node.Source is not null) { Bind(node.Source); } //NOTE: populates Aliases list of the scope (references CTE)

            _scope = _scope.CloseScope();
        }
        private void Bind(in UpdateStatement node)
        {
            _scope = _scope.OpenScope(node);

            if (node.CommonTables is not null)
            {
                Bind(node.CommonTables); //NOTE: populates Tables list of the scope
            }

            //NOTE: binding sequence of Target and Source is important !!!
            //NOTE: see binding algorithms of
            //BindColumn(in ColumnReference column) and
            //_scope.TryGetTableByAlias(in tableAlias, out object table)
            if (node.Target is not null) { Bind(node.Target); } //NOTE: populates Aliases list of the scope
            if (node.Source is not null) { Bind(node.Source); } //NOTE: populates Aliases list of the scope  (references CTE)

            if (node.Set is not null) { Bind(node.Set); }
            if (node.Where is not null) { Bind(node.Where); }

            _scope = _scope.CloseScope();
        }
        private void Bind(in UpsertStatement node)
        {
            _scope = _scope.OpenScope(node);

            if (node.CommonTables is not null)
            {
                Bind(node.CommonTables);
            }

            if (node.Target is not null) { Bind(node.Target); }
            if (node.Source is not null) { Bind(node.Source); }
            if (node.Set is not null) { Bind(node.Set); }
            if (node.Where is not null) { Bind(node.Where); }

            _scope = _scope.CloseScope();
        }
        private void Bind(in SetClause node)
        {
            foreach (SetExpression expression in node.Expressions)
            {
                Bind(in expression);
            }
        }
        private void Bind(in SetExpression node)
        {
            if (node.Column is not null) { Bind(node.Column); }
            if (node.Initializer is not null) { Bind(node.Initializer); }
        }
        private void Bind(in ValuesExpression node)
        {
            foreach (SyntaxNode value in node.Values)
            {
                Bind(in value);
            }
        }
        #endregion
    }
}