using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class VariableReferenceExtractor : IScriptWalker
    {
        private readonly Dictionary<string, VariableReference> _variables = new();
        public List<VariableReference> Extract(in SyntaxNode node)
        {
            ScriptWalker.Walk(in node, this);

            return _variables.Values.ToList();
        }
        public void SayHello(in SyntaxNode node)
        {
            if (node is VariableReference variable)
            {
                if (!_variables.ContainsKey(variable.Identifier))
                {
                    _variables.Add(variable.Identifier, variable);
                }
            }
        }
        public void SayGoodbye(in SyntaxNode node)
        {
            // do nothing
        }
    }
}