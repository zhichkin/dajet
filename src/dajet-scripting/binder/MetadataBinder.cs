using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class MetadataBinder
    {
        private BindingScope _scope;
        private List<string> _errors;
        private IMetadataProvider _schema;
        public bool TryBind(in SyntaxNode node, in IMetadataProvider schema, out BindingScope scope, out List<string> errors)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            _schema = schema; //NOTE: non-database types and objects do not need it
            
            _errors = new List<string>();

            _scope = new BindingScope() { Owner = node };

            try
            {
                Bind(in node);
            }
            catch (Exception exception)
            {
                _errors.Add(ExceptionHelper.GetErrorMessage(exception));
            }

            scope = _scope; //NOTE: test purposes
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
            else if (node is ProduceStatement produce_statement) { Bind(in produce_statement); }
            else if (node is RequestStatement request_statement) { Bind(in request_statement); }
            else if (node is ImportStatement import_statement) { Bind(in import_statement); }
            else if (node is UseStatement use_statement) { Bind(in use_statement); }
            else if (node is ForStatement for_each) { Bind(in for_each); }
            else if (node is CreateTypeStatement udt) { Bind(in udt); }
            else if (node is ApplySequenceStatement apply_sequence) { Bind(in apply_sequence); }
            else if (node is RevokeSequenceStatement revoke_sequence) { Bind(in revoke_sequence); }
            else if (node is AssignmentStatement assignment) { Bind(in assignment); }
            else if (node is CaseStatement case_statement) { Bind(in case_statement); }
            else if (node is IfStatement if_statement) { Bind(in if_statement); }
            else if (node is TryStatement try_statement) { Bind(in try_statement); }
            else if (node is WhileStatement while_statement) { Bind(in while_statement); }
            else if (node is SleepStatement sleep_statement) { Bind(in sleep_statement); }
            else if (node is ReturnStatement return_statement) { Bind(in return_statement); }
            else if (node is ThrowStatement throw_statement) { Bind(in throw_statement); }
            else if (node is StatementBlock statement_block) { Bind(in statement_block); }
            else if (node is PrintStatement print) { Bind(in print); }
            else if (node is ProcessStatement process) { Bind(in process); }
            else if (node is ExecuteStatement execute) { Bind(in execute); } // nothing to bind
            else if (node is WaitStatement wait) { Bind(in wait); }
            else if (node is ModifyStatement modify) { Bind(in modify); } // nothing to bind
        }
        private void RegisterBindingError(TokenType token, string identifier)
        {
            _errors.Add($"Failed to bind [{token}: {identifier}]");
        }

        #region "GLOBAL SCOPE BINDING"
        private void Bind(in ScriptModel node)
        {
            _scope ??= new BindingScope() { Owner = node };

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
                //NOTE: bool, decimal, int, DateTime, string, byte[], Guid, Entity, Union, Array, object

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
            else if (_schema is not null) // UserDefinedType (database UDT) or ApplicationObject (Справочник.Номенклатура)
            {
                MetadataObject table = _schema.GetMetadataObject(node.Identifier);

                if (table is UserDefinedType definition)
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
            if (node.Type.Token == TokenType.Array || node.Type.Token == TokenType.Object)
            {
                if (node.Type.Binding is List<ColumnExpression>) // bind schema only once !?
                {
                    if (!_scope.Variables.ContainsKey(node.Name))
                    {
                        _scope.Variables.Add(node.Name, node.Type); return;
                    }
                }
            }

            Bind(node.Type);

            if (node.Type.Binding is UserDefinedType definition)
            {
                definition.TableName = node.Name; // user-defined type (table-valued parameter)
            }
            else if (node.Type.Binding is Type type)
            {
                if (type == typeof(Entity)) // DECLARE @Ссылка entity
                {
                    if (node.Initializer is null)
                    {
                        node.Type.Binding = Entity.Undefined; // {0:00000000-0000-0000-0000-000000000000}
                    }
                    else if (node.Initializer is ScalarExpression scalar) // DECLARE @Ссылка entity = {code:uuid}
                    {
                        if (Entity.TryParse(scalar.Literal, out Entity entity))
                        {
                            node.Type.Binding = entity;
                        }
                        else
                        {
                            RegisterBindingError(node.Token, node.Name);
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
                    else
                    {
                        throw new FormatException($"[DECLARE {node.Name} entity] unsupported initializer");
                    }
                }
                else if (node.Initializer is SelectExpression select)
                {
                    Bind(in select);

                    if (type == typeof(Guid))
                    {
                        //TODO: validate SELECT expression for different simple data types
                        //EXAMPLE: DECLARE @uuid uuid = SELECT NEWUUID()
                    }
                    else
                    {
                        node.Type.Binding = select.Columns; // object schema
                    }
                }
                else if (node.Initializer is TableUnionOperator union)
                {
                    Bind(in union);

                    node.Type.Binding = (union.Expression1 as SelectExpression).Columns; // object schema
                }
            }
            else if (node.Type.Binding is Entity) // DECLARE @Ссылка Справочник.Номенклатура
            {
                if (node.Initializer is SelectExpression select)
                {
                    Bind(in select);
                }
            }

            // join current scope

            if (node.Type.Token == TokenType.Array || node.Type.Token == TokenType.Object)
            {
                _scope.Variables.Add(node.Name, node.Type); //NOTE: Binding is used to define data schema
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
            List<string> members = ParserHelper.GetAccessMembers(node.Identifier);

            string target = members[0];
            
            object binding = _scope.GetVariableBinding(target);

            string member = members[1].StartsWith('[') ? members[2] : members[1];

            if (binding is TypeIdentifier type && type.Binding is List<ColumnExpression> columns)
            {
                for (int i = 0; i < columns.Count; i++)
                {
                    if (DataMapper.TryMap(columns[i], out string name, out UnionType union))
                    {
                        if (name == member)
                        {
                            node.Binding = UnionType.MapToType(in union);

                            if (union.IsUndefined && columns[i].Expression is VariableReference variable)
                            {
                                if (variable.Binding is TypeIdentifier schema)
                                {
                                    if (schema.Token == TokenType.Object || schema.Token == TokenType.Array)
                                    {
                                        node.Binding = variable; // искомое свойство имеет объектный тип данных
                                    }
                                }
                            }

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

            BindingScope root = _scope.GetRoot();

            root.Tables.Add(node.Table.Identifier, table);
        }
        #endregion

        #region "SELECT AND TABLE BINDING"
        private void Bind(in SelectStatement node)
        {
            ValidateStatement(in node);

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

            if (node.From is not null)
            {
                var appends = new AppendOperatorExtractor().Extract(node.From);

                foreach (var append in appends)
                {
                    BindAppend(append); //FIXME: recursive multiple times binding when nested APPEND
                }
            }

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

            //NOTE: delay binding till INTO clause has been binded
            if (node.Token == TokenType.APPEND) { return; }
            
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

            //NOTE: UNION root order clause
            if (node.Order is OrderClause order)
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
            // see DeclareStatement binding to UserDefinedType
            if (node.Binding is null)
            {
                node.Binding = _scope.GetVariableBinding(node.Identifier);
            }

            // 3. try bind database schema table
            if (node.Binding is null && _schema is not null)
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
        private void BindAppend(in TableJoinOperator node)
        {
            if (node.Token == TokenType.APPEND)
            {
                Bind(node.Expression2);
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

            if (_schema is not null && _schema.TryGetEnumValue(column.Identifier, out EnumValue value) && value is not null)
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
            if (!string.IsNullOrEmpty(node.Target))
            {
                BindStreamConsume(in node);
            }
            else
            {
                BindDatabaseConsume(in node);
            }
        }
        private void BindDatabaseConsume(in ConsumeStatement node)
        {
            ValidateStatement(in node);

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

            if (node.From is not null)
            {
                var appends = new AppendOperatorExtractor().Extract(node.From);

                foreach (var append in appends)
                {
                    BindAppend(append); //FIXME: recursive binding (nested APPEND)
                }
            }

            _scope = _scope.CloseScope();
        }
        private void BindStreamConsume(in ConsumeStatement node)
        {
            _scope = _scope.OpenScope(node);

            for (int i = 0; i < node.Options.Count; i++)
            {
                Bind(node.Options[i]);
            }

            for (int i = 0; i < node.Columns.Count; i++)
            {
                Bind(node.Columns[i]);
            }

            _scope = _scope.CloseScope();
        }
        private void Bind(in ProduceStatement node)
        {
            _scope = _scope.OpenScope(node);

            for (int i = 0; i < node.Options.Count; i++)
            {
                Bind(node.Options[i]);
            }

            for (int i = 0; i < node.Columns.Count; i++)
            {
                Bind(node.Columns[i]);
            }

            _scope = _scope.CloseScope();
        }
        private void Bind(in RequestStatement node)
        {
            _scope = _scope.OpenScope(node);

            if (node.When is not null) { Bind(node.When); }

            for (int i = 0; i < node.Headers.Count; i++)
            {
                Bind(node.Headers[i]);
            }

            for (int i = 0; i < node.Options.Count; i++)
            {
                Bind(node.Options[i]);
            }

            if (node.Response is not null) { Bind(node.Response); }

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

            //FIXME: binding columns referencing CTE fails (because of lookup is done on table aliases)

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
            if (node.Output is not null) { Bind(node.Output); } //NOTE: INTO clause is included

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

        #region "DDL STATEMENT BINDING"
        private void Bind(in CreateTypeStatement node)
        {
            ColumnDefinition column;

            for (int i = 0; i < node.Columns.Count; i++)
            {
                column = node.Columns[i];

                if (column.Type is not null)
                {
                    Bind(column.Type);
                }
            }
        }
        #endregion

        #region "APPLY AND REVOKE SEQUENCE"
        private void Bind(in ApplySequenceStatement node)
        {
            Bind(node.Table);

            BindColumn(node.Table.Binding, node.Column.Identifier, node.Column);
        }
        private void Bind(in RevokeSequenceStatement node)
        {
            Bind(node.Table);
        }
        #endregion

        private void ValidateStatement(in SelectStatement node)
        {
            if (node.Expression is SelectExpression select)
            {
                ValidateAppendOperator(select.From, select.Into);
            }
            else if (node.Expression is TableUnionOperator union)
            {
                var append = new AppendOperatorExtractor().Extract(union);

                if (append.Count > 0)
                {
                    throw new FormatException("[APPEND] operator is not allowed with UNION");
                }
            }
        }
        private void ValidateStatement(in ConsumeStatement node)
        {
            ValidateAppendOperator(node.From, node.Into);
        }
        private void ValidateAppendOperator(in FromClause from, in IntoClause into)
        {
            if (from is null) { return; }

            var append = new AppendOperatorExtractor().Extract(from);

            if (append.Count > 0)
            {
                if (into is null)
                {
                    throw new FormatException("[APPEND] INTO keyword expected.");
                }
                else if (into.Value is null)
                {
                    throw new FormatException("[APPEND] INTO value expected.");
                }
                else if (!into.Value.Identifier.StartsWith('@'))
                {
                    throw new FormatException("[APPEND] INTO variable reference expected.");
                }
            }
        }

        #region "DAJET SCRIPT STATEMENTS"
        private void Bind(in UseStatement node)
        {
            Bind(node.Statements);
        }
        private void Bind(in AssignmentStatement node)
        {
            // Оператор присваивания SET обрабатывается аналогично предложению INTO команды SELECT
            // Для переменных типов object и array необходимо привязать схему данных (структуру) 

            Bind(node.Initializer); // оператор присваивания выполняется справа налево

            Bind(node.Target); // получаем привязку типа данных для переменной

            if (node.Target is VariableReference variable)
            {
                if (variable.Binding is TypeIdentifier type) // object | array
                {
                    if (type.Token == TokenType.Object || type.Token == TokenType.Array)
                    {
                        if (node.Initializer is SelectExpression select)
                        {
                            type.Binding = select.Columns; // object schema definition
                        }
                        else if (node.Initializer is TableUnionOperator union)
                        {
                            type.Binding = (union.Expression1 as SelectExpression).Columns; // object schema definition
                        }
                    }
                }
            }
        }
        private void Bind(in StatementBlock node)
        {
            if (node is null) { return; }

            foreach (SyntaxNode statement in node.Statements)
            {
                Bind(in statement);
            }
        }
        private void Bind(in IfStatement node)
        {
            Bind(node.IF);
            
            Bind(node.THEN);

            if (node.ELSE is not null)
            {
                Bind(node.ELSE);
            }
        }
        private void Bind(in ForStatement node)
        {
            Bind(node.Iterator); // bind to array variable
            Bind(node.Variable); // bind to object variable

            if (node.Iterator.Binding is not TypeIdentifier itype || itype.Token != TokenType.Array)
            {
                RegisterBindingError(node.Token, node.Iterator.Identifier); return;
            }

            if (node.Variable.Binding is not TypeIdentifier vtype || vtype.Token != TokenType.Object)
            {
                RegisterBindingError(node.Token, node.Variable.Identifier); return;
            }

            vtype.Binding = itype.Binding; // columns schema
        }
        private void Bind(in TryStatement node)
        {
            Bind(node.TRY);

            if (node.CATCH is not null) { Bind(node.CATCH); }

            if (node.FINALLY is not null) { Bind(node.FINALLY); }
        }
        private void Bind(in CaseStatement node)
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
        private void Bind(in WhileStatement node)
        {
            Bind(node.Condition);
            Bind(node.Statements);
        }
        private void Bind(in ReturnStatement node)
        {
            Bind(node.Expression);
        }
        private void Bind(in ThrowStatement node)
        {
            Bind(node.Expression);
        }
        private void Bind(in SleepStatement node) { /* nothing to bind */ }
        private void Bind(in PrintStatement node)
        {
            Bind(node.Expression);
        }
        private void Bind(in ExecuteStatement node)
        {
            for (int i = 0; i < node.Parameters.Count; i++)
            {
                Bind(node.Parameters[i]);
            }

            if (node.Return is not null) { Bind(node.Return); }
        }
        private void Bind(in ProcessStatement node)
        {
            for (int i = 0; i < node.Variables.Count; i++)
            {
                Bind(node.Variables[i]);
            }

            if (node.Return is not null) { Bind(node.Return); }

            if (node.Options is not null) // optional
            {
                for (int i = 0; i < node.Options.Count; i++)
                {
                    Bind(node.Options[i]);
                }
            }
        }
        private void Bind(in WaitStatement node)
        {
            if (node.Result is not null)
            {
                Bind(node.Result);
            }

            Bind(node.Tasks);

            if (node.Tasks.Binding is not TypeIdentifier type || type.Token != TokenType.Array)
            {
                RegisterBindingError(node.Token, node.Tasks.Identifier); return;
            }
        }
        private void Bind(in ModifyStatement node) { /* nothing to bind */ }
        #endregion
    }
}