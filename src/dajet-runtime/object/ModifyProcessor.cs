using DaJet.Data;
using DaJet.Scripting;
using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class ModifyProcessor : IProcessor
    {
        private IProcessor _next;
        private readonly ScriptScope _scope;
        private readonly ModifyStatement _statement;
        private readonly string _target;
        private readonly string _source;
        private readonly HashSet<string> _delete;
        public ModifyProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ModifyStatement statement)
            {
                throw new ArgumentException(nameof(ModifyStatement));
            }

            _statement = statement;

            _target = _statement.Target.Identifier;

            if (_statement.Source is not null)
            {
                _source = _statement.Source.Identifier;
            }

            if (_statement.Delete.Count > 0)
            {
                _delete = new HashSet<string>(_statement.Delete.Count);

                foreach (ColumnReference column in _statement.Delete)
                {
                    _delete.Add(column.Identifier);
                }
            }

            if (!_scope.TryGetDeclaration(in _target, out _, out DeclareStatement declare))
            {
                throw new InvalidOperationException($"[MODIFY] Declaration of {_target} is not found");
            }

            if (declare.Type.Binding is not List<ColumnExpression>)
            {
                declare.Type.Binding = DefineTargetObjectSchema();
            }
        }
        public void Dispose() { _next?.Dispose(); }
        public void Synchronize() { _next?.Synchronize(); }
        public void LinkTo(in IProcessor next) { _next = next; }
        private List<ColumnExpression> DefineTargetObjectSchema()
        {
            List<ColumnExpression> schema = new();

            if (!string.IsNullOrEmpty(_source))
            {
                if (!_scope.TryGetDeclaration(in _source, out _, out DeclareStatement declare))
                {
                    throw new InvalidOperationException($"[MODIFY] Declaration of {_source} is not found");
                }

                if (declare.Type.Binding is List<ColumnExpression> columns)
                {
                    foreach (ColumnExpression column in columns)
                    {
                        if (_delete is not null && _delete.Contains(column.Alias))
                        {
                            continue;
                        }

                        schema.Add(new ColumnExpression()
                        {
                            Alias = column.Alias, // TODO: infer column data type
                            Expression = new ScalarExpression() { Token = TokenType.String }
                        });
                    }
                }
            }

            foreach (ColumnExpression column in _statement.Select)
            {
                schema.Add(new ColumnExpression()
                {
                    Alias = column.Alias, // TODO: infer column data type
                    Expression = new ScalarExpression() { Token = TokenType.String }
                });
            }

            return schema;
        }
        public void Process()
        {
            if (!_scope.TryGetValue(in _target, out object value))
            {
                throw new InvalidOperationException($"[MODIFY] variable {_target} is not defined");
            }

            if (value is not DataObject target)
            {
                target = new DataObject(_statement.Select.Count);

                if (!_scope.TrySetValue(in _target, target))
                {
                    throw new InvalidOperationException($"[MODIFY] Error setting object {_target}");
                }
            }

            ModifyFromSourceObject(ref target);
            ModifyDeleteTargetObject(in target);
            ModifySelectTargetObject(in target);

            _next?.Process();
        }
        private void ModifyFromSourceObject(ref DataObject target)
        {
            if (string.IsNullOrEmpty(_source)) { return; }

            if (!_scope.TryGetValue(in _source, out object value))
            {
                throw new InvalidOperationException($"[MODIFY] variable {_source} is not defined");
            }

            if (value is null)
            {
                throw new InvalidOperationException($"[MODIFY] variable {_source} value is NULL");
            }

            if (value is not DataObject source)
            {
                throw new InvalidOperationException($"[MODIFY] variable {_source} must be of type {{object}}");
            }

            target = source.Copy(); // create new target object from source

            if (!_scope.TrySetValue(in _target, target))
            {
                throw new InvalidOperationException($"[MODIFY] Error coping from {_source} to {_target}");
            }
        }
        private void ModifyDeleteTargetObject(in DataObject target)
        {
            if (_delete is null) { return; }

            foreach (string identifier in _delete)
            {
                target.Remove(identifier);
            }
        }
        private void ModifySelectTargetObject(in DataObject target)
        {
            if (_statement.Select is null) { return; }

            foreach (ColumnExpression property in _statement.Select)
            {
                string propertyName = property.Alias;

                if (!StreamFactory.TryEvaluate(in _scope, property.Expression, out object value))
                {
                    throw new InvalidOperationException($"[MODIFY] Error evaluating property {propertyName} value");
                }

                target.SetValue(propertyName, value);
            }
        }
        private void ConfigureTargetObjectSchema()
        {
            if (_statement.Target is null) { return; }

            string identifier = _statement.Target.Identifier;

            if (!_scope.TryGetDeclaration(in identifier, out _, out DeclareStatement target))
            {
                throw new InvalidOperationException($"Declaration of {identifier} is not found");
            }
        }
    }
}