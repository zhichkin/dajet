using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class ScriptScope
    {
        public ScriptScope() { }
        public ScriptScope(SyntaxNode owner, ScriptScope parent)
        {
            Owner = owner;
            Parent = parent;
        }
        public SyntaxNode Owner { get; set; }
        public ScriptScope Parent { get; set; }
        public List<ScriptScope> Children { get; } = new();

        public List<SyntaxNode> Identifiers { get; } = new(); //TODO: remove deprecated algorithm
        public Dictionary<string, object> Tables { get; } = new(); // CTE (common table expression) or temporary tables
        public Dictionary<string, object> Aliases { get; } = new(); // table expression (subquery) or schema tables
        public Dictionary<string, object> Columns { get; } = new();
        public Dictionary<string, object> Variables { get; } = new(); // table variables or UDT (user-defined type)

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
        public ScriptScope Descendant<TOwner>() where TOwner : SyntaxNode
        {
            return null; //TODO: getting descendant scope
        }
        public override string ToString()
        {
            return $"Owner: {Owner}";
        }



        public ScriptScope OpenScope(in SyntaxNode owner)
        {
            ScriptScope scope = new(owner, this);

            if (owner is SelectExpression select && select.IsCorrelated)
            {
                //TODO: find parent scope using correlation logic
            }

            Children.Add(scope);
            
            return scope;
        }
        public ScriptScope CloseScope()
        {
            return Parent;
        }

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
            if (string.IsNullOrEmpty(alias) ||
                alias.ToLowerInvariant() == "deleted" ||
                alias.ToLowerInvariant() == "inserted")
            {
                // TODO: find all candidate tables and warn ambiguous names

                // take first available table
                table = Aliases.Values.FirstOrDefault();
                return (table is not null);
            }

            // 1. Lookup current scope

            ScriptScope scope = Ancestor<SelectExpression>();

            if (scope is not null && scope.Aliases.TryGetValue(alias, out table))
            {
                return true;
            }

            // 2. Lookup correlated scope

            while (scope is not null)
            {
                if (scope.Owner is SelectExpression select)
                {
                    if (select.IsCorrelated && scope.Parent is not null)
                    {
                        scope = scope.Parent.Ancestor<SelectExpression>();

                        if (scope is not null && scope.Aliases.TryGetValue(alias, out table))
                        {
                            return true;
                        }
                    }
                }

                scope = scope.Parent;
            }

            table = null;
            return false;
        }
    }
}