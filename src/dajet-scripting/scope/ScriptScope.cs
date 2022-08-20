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
        public SyntaxNode Owner { get; set; } = null!;
        public ScriptScope Parent { get; set; } = null!;
        public List<ScriptScope> Children { get; } = new();
        public List<SyntaxNode> Identifiers { get; } = new();
        public ScriptScope Root
        {
            get
            {
                ScriptScope root = this;

                while (root.Parent != null)
                {
                    root = root.Parent;
                }

                return root;
            }
        }
        public override string ToString()
        {
            return $"{Type}: {Owner}";
        }
    }
}