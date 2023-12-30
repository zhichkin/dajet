using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class SchemaBinder
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
            if (node is ScriptModel script) { Bind(in script); }
            else if (node is CommentStatement comment) { Bind(in comment); }
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
            else if (node is ScalarExpression scalar) { Bind(in scalar); }
            else if (node is VariableReference variable) { Bind(in variable); }
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
        private void Bind(in ScriptModel node)
        {
            _scope ??= new ScriptScope() { Owner = node };

            foreach (SyntaxNode statement in node.Statements)
            {
                Bind(in statement);
            }
        }
        private void Bind(in ApplicationObject entity) { }
        private void Bind(in MetadataProperty property) { }
        private void Bind(in MetadataColumn column) { }
        private void Bind(in CommentStatement node) { }
        private void Bind(in DeclareStatement node)
        {
            //TODO: DeclareStatement.Initializer ... resolve TableExpression

            if (node.Type is null) { return; }

            Bind(node.Type);

            if (node.Type.Binding is EntityDefinition definition)
            {
                definition.TableName = node.Name; // user-defined type (table-valued parameter)
            }
            else if (node.Type.Binding is Type type && type == typeof(Entity))
            {
                if (node.Initializer is ScalarExpression scalar)
                {
                    if (Entity.TryParse(scalar.Literal, out Entity entity))
                    {
                        node.Type.Binding = entity;
                    }
                }
            }

            _scope.Variables.Add(node.Name, node.Type.Binding); // join current scope
        }
        private void Bind(in TypeIdentifier node)
        {
            //TODO: bind "union" type identifier

            if (ScriptHelper.IsDataType(node.Identifier, out Type type))
            {
                // bool, decimal, DateTime, string, byte[], Guid, Entity, Union
                node.Binding = type;
            }
            else // EntityDefinition (table)
            {
                MetadataObject table = _schema.GetMetadataObject(node.Identifier);

                if (table is EntityDefinition definition)
                {
                    node.Binding = definition;
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
        private void Bind(in VariableReference node)
        {
            node.Binding = _scope.GetVariableBinding(node.Identifier);

            if (node.Binding is null)
            {
                RegisterBindingError(node.Token, node.Identifier);
            }
        }
        private void Bind(in ScalarExpression node) { }
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

        private void Bind(in SelectStatement node)
        {
            _scope = _scope.OpenScope(node);

            if (node.CommonTables is not null)
            {
                Bind(node.CommonTables);
            }

            Bind(node.Select); //NOTE: SelectExpression

            _scope = _scope.CloseScope();
        }
        private void Bind(in CommonTableExpression node)
        {
            //NOTE: common table expression can be recursive and reference itself

            if (node.Next is not null)
            {
                Bind(node.Next);
            }

            _scope.Tables.Add(node.Name, node); // join current SelectStatement scope

            Bind(node.Expression); //NOTE: { SelectExpression | INSERT | UPDATE | DELETE }
        }
        private void Bind(in SelectExpression node)
        {
            _scope = _scope.OpenScope(node);

            if (node.From is not null) { Bind(node.From); }
            
            for (int i = 0; i < node.Select.Count; i++)
            {
                Bind(node.Select[i]);
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
                _scope.Tables.Add(node.Alias, node); // join current SelectExpression scope
            }
        }
        private void Bind(in TableJoinOperator node)
        {
            Bind(node.Expression1); //NOTE: { TableReference | TableExpression | TableJoinOperator }
            Bind(node.Expression2);
            Bind(node.On);
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
        }
        private void Bind(in TableReference node)
        {
            //TODO: join table to the current scope by alias !!!

            // try bind common table expression, temporary table or table variable
            node.Binding = _scope.GetTableBinding(node.Identifier);

            // try bind table user-defined type (table-valued parameter)
            // see DeclareStatement binding and EntityDefinition
            node.Binding ??= _scope.GetVariableBinding(node.Identifier);

            if (node.Binding is not null)
            {
                _scope.Tables.Add(node.Alias, node.Binding); // join current SelectExpression scope

                return; // successful binding
            }

            // try bind database schema table

            MetadataObject schema = null;

            try
            {
                schema = _schema.GetMetadataObject(node.Identifier);
            }
            catch
            {
                RegisterBindingError(node.Token, node.Identifier);
            }

            if (schema is ApplicationObject entity)
            {
                node.Binding = entity; // successful binding

                _scope.Tables.Add(node.Alias, node.Binding); // join current SelectExpression scope
            }
            else
            {
                RegisterBindingError(node.Token, node.Identifier);
            }
        }

        private void Bind(in StarExpression node) { }
        private void Bind(in ColumnReference node)
        {
            if (node.Binding is MetadataProperty source)
            {
                Bind(in source);
            }
            else if (node.Binding is MetadataColumn column)
            {
                Bind(in column);
            }
            else if (node.Binding is ColumnExpression parent)
            {
                Bind(in parent);
            }
        }
        private void Bind(in ColumnExpression node)
        {
            Bind(node.Expression);
        }
        
        
        
        private void Bind(in TopClause node)
        {
            Bind(node.Expression);
        }
        private void Bind(in FromClause node)
        {
            Bind(node.Expression);
        }
        private void Bind(in IntoClause node)
        {
            Bind(node.Table);

            //NOTE: columns are derived from SELECT expression
            //foreach (ColumnExpression column in node.Columns)
            //{
            //    Bind(in column);
            //}
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
        private void Bind(in ConsumeStatement node)
        {
            if (node.Top is not null) { Bind(node.Top); }

            for (int i = 0; i < node.Columns.Count; i++)
            {
                Bind(node.Columns[i]);
            }

            if (node.From is not null) { Bind(node.From); }
            if (node.Where is not null) { Bind(node.Where); }
            if (node.Order is not null) { Bind(node.Order); }
        }
        private void Bind(in ImportStatement node)
        {
            throw new NotImplementedException();
        }
        private void Bind(in OutputClause node)
        {
            throw new NotImplementedException();
        }
        private void Bind(in DeleteStatement node)
        {
            throw new NotImplementedException();
        }
        private void Bind(in InsertStatement node)
        {
            throw new NotImplementedException();
        }
        private void Bind(in SetClause node)
        {
            throw new NotImplementedException();
        }
        private void Bind(in SetExpression node)
        {
            throw new NotImplementedException();
        }
        private void Bind(in UpdateStatement node)
        {
            throw new NotImplementedException();
        }
        private void Bind(in UpsertStatement node)
        {
            throw new NotImplementedException();
        }
        private void Bind(in ValuesExpression node)
        {
            throw new NotImplementedException();
        }
    }
}