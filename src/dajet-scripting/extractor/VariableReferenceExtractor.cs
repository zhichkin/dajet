using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class VariableReferenceExtractor : IScriptWalker
    {
        private readonly List<string> _variables = new();
        public List<string> Extract(in SyntaxNode node)
        {
            ScriptWalker.Walk(in node, this);

            return _variables;
        }
        public void SayHello(in SyntaxNode node)
        {
            if (node is VariableReference variable)
            {
                if (!_variables.Contains(variable.Identifier))
                {
                    _variables.Add(variable.Identifier[1..]);
                }
            }
        }
        public void SayGoodbye(in SyntaxNode node)
        {
            // do nothing
        }
    }
}