using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class ScriptScope
    {
        ///<summary>Иерархия пространства видимости (физическая)</summary>
        private readonly ScriptScope _ancestor; //TODO: encapsulate logic in OpenScope method or class
        public ScriptScope() { }
        public ScriptScope(SyntaxNode owner, ScriptScope parent)
        {
            Owner = owner;
            Parent = parent; //NOTE: can be overriden in OpenScope method !!!
            _ancestor = parent; //NOTE: is used by CloseScope method
        }
        public SyntaxNode Owner { get; set; }
        ///<summary>Иерархия пространства видимости (логическа)</summary>
        public ScriptScope Parent { get; set; }
        ///<summary>Дочерние пространства видимости (логические)</summary>
        public List<ScriptScope> Children { get; } = new();
        public Dictionary<string, object> Tables { get; } = new(); // CTE (common table expression) or temporary tables
        public Dictionary<string, object> Aliases { get; } = new(); // table expression (subquery) or schema tables
        public Dictionary<string, object> Columns { get; } = new(); //NOTE: used for diagnosic purposes
        public Dictionary<string, object> Variables { get; } = new(); // table variables or UDT (user-defined type)
        public override string ToString() { return $"Owner: {Owner}"; }

        public ScriptScope GetRoot()
        {
            ScriptScope root = this;

            while (root.Parent is not null)
            {
                root = root.Parent;
            }

            return root;
        }
        public ScriptScope Ancestor<TOwner>() where TOwner : SyntaxNode
        {
            Type type = typeof(TOwner);

            ScriptScope scope = this;
            SyntaxNode owner = Owner;

            while (scope is not null)
            {
                if (owner is not null && owner.GetType() == type)
                {
                    return scope;
                }

                scope = scope.Parent;
                owner = scope?.Owner;
            }

            return null;
        }
        public ScriptScope NewScope(in SyntaxNode owner)
        {
            ScriptScope scope = new(owner, this);
            
            Children.Add(scope);
            
            return scope;
        }
        public ScriptScope OpenScope(in SyntaxNode owner)
        {
            ScriptScope scope = new(owner, this);

            if (owner is not SelectExpression select || select.IsCorrelated)
            {
                Children.Add(scope); return scope;
            }

            ScriptScope parent = this;

            while (parent is not null)
            {
                select = parent.Owner as SelectExpression;

                if (select is null)
                {
                    scope.Parent = parent;
                    parent.Children.Add(scope);
                    return scope;
                }
                else if (select.IsCorrelated)
                {
                    scope.Parent = parent.Parent;
                    parent.Parent.Children.Add(scope);
                    return scope;
                }

                parent = parent.Parent;
            }

            throw new InvalidOperationException($"Failed to open scope [{owner}]");
        }
        public ScriptScope CloseScope() { return _ancestor; }

        public object GetVariableBinding(in string name)
        {
            ScriptScope scope = this;

            while (scope is not null)
            {
                if (scope.Variables.TryGetValue(name, out object binding))
                {
                    return binding;
                }

                scope = scope.Parent;
            }

            return null;
        }
        public object GetTableBinding(in string name)
        {
            ScriptScope scope = this;

            while (scope is not null)
            {
                if (scope.Tables.TryGetValue(name, out object binding))
                {
                    return binding;
                }

                scope = scope.Parent;
            }

            return null;
        }
        public bool TryGetTableByAlias(in string alias, out object table)
        {
            // TODO: find all candidate tables and warn ambiguous names

            if (string.IsNullOrEmpty(alias) ||
                alias.ToLowerInvariant() == "deleted" ||
                alias.ToLowerInvariant() == "inserted")
            {
                // take first available table
                table = Aliases.Values.FirstOrDefault();
                
                return (table is not null);
            }

            // lookup current and upper scopes

            ScriptScope scope = this;

            while (scope is not null)
            {
                if (scope.Aliases.TryGetValue(alias, out table))
                {
                    return true;
                }

                scope = scope.Parent;
            }

            // failed to bind table by alias

            table = null;
            return false;
        }

        public Dictionary<string, object> Context { get; } = new(); // stream context variables and their values
        public bool TryGetVariableValue(in string name, out object value)
        {
            value = null;

            ScriptScope scope = this;

            while (scope is not null)
            {
                if (scope.Context.TryGetValue(name, out value))
                {
                    return true;
                }

                scope = scope.Parent;
            }

            return false;
        }
        public bool TryGetVariableDeclaration(in string name, out bool local, out DeclareStatement declare)
        {
            local = true;
            declare = null;

            ScriptScope scope = this;

            while (scope is not null)
            {
                if (scope.Variables.TryGetValue(name, out object statement))
                {
                    local = ReferenceEquals(this, scope);
                    
                    declare = statement as DeclareStatement;
                    
                    return declare is not null;
                }

                scope = scope.Parent;
            }

            return false; // not found
        }
        public static bool IsStreamScope(in SyntaxNode statement)
        {
            return statement is UseStatement
                || statement is ForEachStatement
                || statement is ConsumeStatement
                || statement is ProduceStatement
                || statement is SelectStatement select && select.IsStream
                || statement is UpdateStatement update && update.Output?.Into?.Value is not null;
        }
        public static void BuildStreamScope(in ScriptModel script, out ScriptScope scope)
        {
            scope = new ScriptScope() { Owner = script };

            ScriptScope _current = scope;

            for (int i = 0; i < script.Statements.Count; i++)
            {
                SyntaxNode statement = script.Statements[i];

                if (statement is CommentStatement) { continue; }

                if (statement is DeclareStatement declare)
                {
                    _current.Context.Add(declare.Name, null);
                    _current.Variables.Add(declare.Name, declare);
                }
                else if (IsStreamScope(in statement))
                {
                    if (_current.Owner is UseStatement && statement is UseStatement)
                    {
                        _current = _current.CloseScope(); // one database context closes another
                    }
                    _current = _current.NewScope(in statement); // create parent scope
                }
                else
                {
                    _ = _current.NewScope(in statement); // add child to parent scope
                }
            }
        }
    }
}