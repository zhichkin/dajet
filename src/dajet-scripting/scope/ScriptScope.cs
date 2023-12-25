using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class ScriptScope
    {
        public ScriptScope() { }
        public ScriptScope(ScopeType type, SyntaxNode owner, ScriptScope parent)
        {
            Type = type;
            Owner = owner;
            Parent = parent;
        }
        public ScopeType Type { get; set; } = ScopeType.Global;
        public SyntaxNode Owner { get; set; }
        public ScriptScope Parent { get; set; }
        public List<ScriptScope> Children { get; } = new();
        public List<SyntaxNode> Identifiers { get; } = new();
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
            return $"{Type}: {Owner}";
        }
    }
}