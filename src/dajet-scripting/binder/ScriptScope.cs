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
    }
}