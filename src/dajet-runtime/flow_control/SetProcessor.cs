using DaJet.Metadata;
using DaJet.Scripting;
using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class SetProcessor : IProcessor
    {
        private IProcessor _next;
        private IProcessor _init;
        private readonly TokenType _token;
        private readonly ScriptScope _scope;
        private readonly VariableReference _target;
        private readonly AssignmentStatement _statement;
        public SetProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not AssignmentStatement statement)
            {
                throw new ArgumentException(nameof(AssignmentStatement));
            }

            _statement = statement;

            if (_statement.Target is not VariableReference variable)
            {
                throw new InvalidOperationException($"[SET] target is not variable");
            }

            _target = variable;

            if (!_scope.TryGetDeclaration(_target.Identifier, out _, out DeclareStatement declare))
            {
                throw new FormatException($"[SET] variable declaration is not found {_target}");
            }

            SyntaxNode initializer = _statement.Initializer;

            if (initializer is SelectExpression select && !StreamFactory.IsDatabaseSelect(in select))
            {
                StreamFactory.BindVariables(in _scope); // runtime binding
            }
            else if (initializer is SelectExpression || initializer is TableUnionOperator)
            {
                if (!_scope.TryGetMetadataProvider(out IMetadataProvider database, out string error))
                {
                    throw new InvalidOperationException(error);
                }

                SelectExpression select_expression = null;

                if (initializer is SelectExpression _select)
                {
                    select_expression = _select;
                }
                else if (initializer is TableUnionOperator union)
                {
                    select_expression = union.Expression1 as SelectExpression;
                }

                select_expression.Into = new IntoClause()
                {
                    Value = _target,
                    Columns = select_expression.Columns
                };

                SelectStatement select_command = new()
                {
                    Expression = initializer
                };

                StatementBlock block = new();
                block.Statements.Add(select_command);

                ScriptScope select_scope = _scope.Create(block);

                StreamFactory.InitializeVariables(in select_scope, in database); // database binding

                _init = StreamFactory.CreateStream(in select_scope);
            }
            else // runtime expression
            {
                StreamFactory.BindVariables(in _scope); // runtime binding
            }

            if (variable.Binding is Type type)
            {
                if (type == typeof(bool)) { _token = TokenType.Boolean; }
                else if (type == typeof(int)) { _token = TokenType.Number; }
                else if (type == typeof(long)) { _token = TokenType.Number; }
                else if (type == typeof(decimal)) { _token = TokenType.Number; }
                else if (type == typeof(DateTime)) { _token = TokenType.DateTime; }
                else if (type == typeof(string)) { _token = TokenType.String; }
                else if (type == typeof(byte[])) { _token = TokenType.Binary; }
                else if (type == typeof(Guid)) { _token = TokenType.Uuid; }
                else
                {
                    throw new FormatException($"[SET] invalid variable {_target} binding {type}");
                }
            }
            else if (variable.Binding is Entity entity)
            {
                _token = TokenType.Entity;
            }
            else
            {
                _token = declare.Type.Token; // object | array
            }
        }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Dispose() { _next?.Dispose(); }
        public void Synchronize() { _next?.Synchronize(); }
        public void Process()
        {
            object value = null;

            SyntaxNode initializer = _statement.Initializer;

            if (_init is not null) // SELECT ... INTO <variable> FROM <database>
            {
                _init.Process(); // scalar | object | array

                if (!_scope.TryGetValue(_target.Identifier, out value))
                {
                    throw new InvalidOperationException($"[SET] failed to get variable value {_target}");
                }
            }
            else if (initializer is SelectExpression select && select.From is null && _token == TokenType.Object)
            {
                value = StreamFactory.ConstructObject(in _scope, in select);
            }
            else if (!StreamFactory.TryEvaluate(in _scope, in initializer, out value))
            {
                throw new InvalidOperationException($"[SET] failed to evaluate initializer {_target}");
            }

            if (!_scope.TrySetValue(_target.Identifier, in value))
            {
                throw new InvalidOperationException($"[SET] failed to assign variable {_target}");
            }

            _next?.Process();
        }
    }
}